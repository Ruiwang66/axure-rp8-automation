using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Runs the original RP8 entry point in a dedicated visible process.
// Commands execute on its own dispatcher; no simulated desktop input.
internal static class RP8Live {
    const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static readonly JavaScriptSerializer Json=new JavaScriptSerializer {MaxJsonLength=16000000};
    static string Root, Home, Session, Requests;
    static Assembly ClientWin, ClientAssembly, Model, Platform;
    static object Client;
    static Window Window;
    static DispatcherTimer Timer;
    static bool Configured;
    static IntPtr Hook;
    static HookProc Callback;
    static int BlockedActivation, BlockedFocus, ManualActivation;
    static readonly HashSet<string> OwnedFiles=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    static System.Threading.Mutex InstanceMutex;
    delegate IntPtr HookProc(int code,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,HookProc callback,IntPtr module,uint thread);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr w,out uint pid);
    [DllImport("user32.dll")] static extern bool GetPhysicalCursorPos(out PointXY point);
    [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();
    [StructLayout(LayoutKind.Sequential)] struct PointXY {public int X,Y;}
    static uint Pid { get {return (uint)System.Diagnostics.Process.GetCurrentProcess().Id;} }
    static object Get(object obj,string name) {
        if(obj==null) return null;
        var prop=obj.GetType().GetProperty(name,Any);
        if(prop!=null)return prop.GetValue(obj,null);
        var getter=obj.GetType().GetMethods(Any).FirstOrDefault(m=>m.Name=="get_"+name&&m.GetParameters().Length==0);
        if(getter!=null)return getter.Invoke(obj,null);
        throw new MissingMemberException(obj.GetType().FullName,name);
    }
    static void Set(object obj,string name,object value) {obj.GetType().GetProperty(name,Any).GetSetMethod(true).Invoke(obj,new object[]{value});}
    static string Text(IDictionary<string,object> d,string key,string fallback="") {object v;return d.TryGetValue(key,out v)&&v!=null?Convert.ToString(v):fallback;}
    static double Number(IDictionary<string,object> d,string key,double fallback) {
        object v;double n=d.TryGetValue(key,out v)?Convert.ToDouble(v):fallback;
        if(double.IsNaN(n)||double.IsInfinity(n)||Math.Abs(n)>20000)throw new ArgumentException("Invalid "+key);
        return n;
    }
    static void OwnFile() {
        string file=Convert.ToString(Get(Get(Client,"CurrentFile"),"FileName"));
        if(!OwnedFiles.Contains(Path.GetFullPath(file)))throw new InvalidOperationException("Current document is not this instance's assigned working copy");
    }
    static void QueueCurrentPageSave() {
        // Direct model/style updates must join the client's native package-saving queue.
        // TouchDocument only updates the UI dirty flag and does not enqueue the package.
        var package=Get(Get(Client,"CurrentDiagram"),"PageFragment");QueuePackageSave(package);
    }
    static void QueuePackageSave(object package) {
        var field=ClientAssembly.GetType("eq").GetFields(BindingFlags.Instance|BindingFlags.NonPublic)
            .Single(f=>f.FieldType.IsGenericType&&f.FieldType.GetGenericTypeDefinition().FullName=="Axure.Linq.Collections.AxSet`1"&&
                f.FieldType.GetGenericArguments()[0].FullName=="Axure.Platform.ObjectPersistanceContext.PackageObject");
        var queue=field.GetValue(Client);
        queue.GetType().GetMethod("Add",new[]{field.FieldType.GetGenericArguments()[0]}).Invoke(queue,new[]{package});
    }
    static List<object> CurrentWidgets() {
        // The RP8 diagram stores layer descendants in its flat object collection too.
        // Expose roots here, and traverse layers once in AllWidgets to avoid duplicate IDs.
        return ((IEnumerable)Get(Get(Client,"CurrentDiagram"),"DiagramObjects")).Cast<object>().Where(w=>Get(w,"ParentLayer")==null).ToList();
    }
    static IEnumerable<object> Descendants(object widget) {
        yield return widget;
        if(widget.GetType().FullName=="Axure.DocumentModel.Objects.Layer")
            foreach(var child in ((IEnumerable)Get(widget,"DiagramObjects")).Cast<object>())foreach(var item in Descendants(child))yield return item;
    }
    static List<object> AllWidgets() {return CurrentWidgets().SelectMany(Descendants).ToList();}
    static Dictionary<string,object> WidgetInfo(object widget) {
        var rect=Get(widget,"Rect");
        var info=new Dictionary<string,object>{{"id",Convert.ToString(Get(widget,"UniqueIdentifier"))},{"name",Get(widget,"Name")},
            {"type",widget.GetType().FullName},{"text",widget.GetType().GetProperty("Text",Any)==null?null:Get(widget,"Text")},
            {"x",Get(rect,"X")},{"y",Get(rect,"Y")},{"width",Get(rect,"Width")},{"height",Get(rect,"Height")}};
        if(widget.GetType().FullName=="Axure.DocumentModel.Objects.Layer")info["children"]=((IEnumerable)Get(widget,"DiagramObjects")).Cast<object>().Select(WidgetInfo).ToArray();
        return info;
    }
    static void Bounds(object widget,IDictionary<string,object> request) {
        var old=Get(widget,"Rect");double x=Number(request,"x",Convert.ToDouble(Get(old,"X"))),y=Number(request,"y",Convert.ToDouble(Get(old,"Y")));
        double width=Number(request,"width",Convert.ToDouble(Get(old,"Width"))),height=Number(request,"height",Convert.ToDouble(Get(old,"Height")));
        if(width<1||height<1)throw new ArgumentException("Positive width and height required");
        var rect=Activator.CreateInstance(Platform.GetType("Axure.Platform.Drawing.Rect"),new object[]{x,y,width,height});
        Model.ManifestModule.ResolveMethod(100664000).Invoke(widget,new object[]{rect,false});
    }
    static object CreateWidget(IDictionary<string,object> request) {
        OwnFile();var page=Get(Get(Client,"CurrentDiagram"),"PageFragment");
        string kind=Text(request,"kind","rectangle");object widget;
        if(kind=="image") {
            string path=Path.GetFullPath(Text(request,"source"));
            if(!File.Exists(path)||!new[]{".png",".jpg",".jpeg"}.Contains(Path.GetExtension(path).ToLowerInvariant()))throw new ArgumentException("An existing PNG/JPEG is required");
            widget=Activator.CreateInstance(Model.GetType("Axure.DocumentModel.Objects.ImageBox"),new object[]{page});
            var stream=new MemoryStream(File.ReadAllBytes(path),false);
            var reference=Activator.CreateInstance(Platform.GetType("Axure.Platform.ObjectPersistanceContext.HashedImageRef"),new object[]{stream,Path.GetExtension(path),null,false});
            Set(widget,"FitToImage",false);Set(widget,"Image",reference);
            if(request.ContainsKey("crop")) {
                var crop=(IDictionary<string,object>)request["crop"];
                Bounds(widget,new Dictionary<string,object>{{"x",0},{"y",0},{"width",Number(request,"sourceWidth",941)},{"height",Number(request,"sourceHeight",1672)}});
                var region=Activator.CreateInstance(Platform.GetType("Axure.Platform.Drawing.Rect"),new object[]{Number(crop,"x",0),Number(crop,"y",0),Number(crop,"width",1),Number(crop,"height",1)});
                var render=Model.GetType("ex").GetMethods(Any).Single(m=>m.ReturnType.FullName=="Axure.Platform.Drawing.IImage"&&m.GetParameters().Length==4&&m.GetParameters()[0].ParameterType.FullName=="Axure.Platform.Drawing.Rect"&&m.GetParameters()[1].ParameterType.FullName=="Axure.Platform.ObjectPersistanceContext.PageStyleChain");
                var empty=Platform.GetType("Axure.Platform.Drawing.Rect").GetField("EMPTY").GetValue(null);
                var clipped=render.Invoke(widget,new object[]{region,Get(page,"Chain"),empty,double.NaN});
                try {
                    var convert=Platform.GetType("Axure.Platform.ImageFunctions").GetMethods(BindingFlags.Public|BindingFlags.Static).Single(m=>m.Name=="ToHashedImageRef"&&m.GetParameters().Length==4);
                    Set(widget,"Image",convert.Invoke(null,new object[]{clipped,"",1d,true}));
                } finally {((IDisposable)clipped).Dispose();}
            }
        }
        else if(kind=="button"||kind=="input"||kind=="checkbox") widget=Activator.CreateInstance(Model.GetType("Axure.DocumentModel.Objects."+(kind=="button"?"Button":kind=="checkbox"?"Checkbox":"TextBox")),new object[]{page});
        else {
            var shapes=new Dictionary<string,string>{{"rectangle","Rectangle"},{"paragraph","Paragraph"},{"heading","H1"},{"ellipse","Ellipse"},{"vector","Vector"},{"hline","HorizontalLine"},{"vline","VerticalLine"}};
            if(!shapes.ContainsKey(kind))throw new ArgumentException("Unsupported kind: "+kind);
            widget=Activator.CreateInstance(Model.GetType("Axure.DocumentModel.Objects.VectorShape"),new object[]{page,
                Enum.Parse(Platform.GetType("Axure.Platform.Shape"),shapes[kind]),"",Enum.Parse(Model.GetType("Axure.DocumentModel.Objects.VectorLegacyType"),"None")});
        }
        Set(widget,"Name",Text(request,"name",kind));if(widget.GetType().GetProperty("Text",Any)!=null)Set(widget,"Text",Text(request,"text"));Bounds(widget,request);
        if(kind=="vector")RP8Styling.VectorPath(widget,request);
        if(request.ContainsKey("style"))RP8Styling.Apply(widget,(Dictionary<string,object>)request["style"]);
        if(request.ContainsKey("placeholder"))Set(widget,"PlaceholderText",Text(request,"placeholder"));
        if(request.ContainsKey("showBorder"))Set(widget,"ShowBorder",Convert.ToBoolean(request["showBorder"]));
        if(request.ContainsKey("inputType"))Set(widget,"Type",Enum.Parse(Model.GetType("Axure.DocumentModel.Objects.InputType"),Text(request,"inputType")));
        if(kind=="input")Set(widget,"HideHintOnFocus",true);
        if(request.ContainsKey("placeholderColor"))RP8Styling.HintColor(widget,Text(request,"placeholderColor"));
        // Native add-object edit -> workspace ApplyEdit; the canvas and undo stack receive the edit.
        ClientAssembly.ManifestModule.ResolveMethod(100665411).Invoke(Get(Client,"DiagramEditor"),new object[]{widget});
        return WidgetInfo(widget);
    }
    static void Write(string path,object data) {File.WriteAllText(path,Json.Serialize(data),new UTF8Encoding(false));}
    static string Hash(string path) {using(var sha=SHA256.Create())using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","");}
    static Dictionary<string,object> Audit() {
        PointXY p;GetPhysicalCursorPos(out p);uint foregroundPid;var foreground=GetForegroundWindow();GetWindowThreadProcessId(foreground,out foregroundPid);
        return new Dictionary<string,object>{{"foregroundWindow",foreground.ToInt64()},{"foregroundProcessId",foregroundPid},{"cursorX",p.X},{"cursorY",p.Y},
            {"clipboardSequence",GetClipboardSequenceNumber()},{"blockedActivation",BlockedActivation},{"blockedFocus",BlockedFocus},{"manualActivation",ManualActivation}};
    }
    static IntPtr Guard(int code,IntPtr w,IntPtr l) {
        if(code==5) { // HCBT_ACTIVATE: only a real user mouse activation may activate our windows.
            bool mouse=l!=IntPtr.Zero && Marshal.ReadInt32(l)!=0;
            if(mouse) ManualActivation++; else {BlockedActivation++;return new IntPtr(1);}
        }
        if(code==9) { // HCBT_SETFOCUS: never steal focus from another foreground process.
            uint pid;GetWindowThreadProcessId(GetForegroundWindow(),out pid);
            if(pid!=Pid) {BlockedFocus++;return new IntPtr(1);}
        }
        return CallNextHookEx(Hook,code,w,l);
    }
    static object FindClient() {
        if(ClientWin==null) ClientWin=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="Client.Win32");
        if(ClientWin==null) return null;
        var launcher=ClientWin.GetType("Axure.Client.Win32.WPFClientLauncher");
        var field=launcher.GetFields(BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static)
            .Single(f=>f.FieldType.FullName=="Axure.Client.Win32.WPF.Interface.WPFClient");
        return field.GetValue(null);
    }
    static Dictionary<string,object> Status() {
        var status=new Dictionary<string,object>{{"processId",Pid},{"ready",Client!=null&&Window!=null&&Window.IsVisible},{"sessionDirectory",Session},
            {"windowHandle",Window==null?0:new WindowInteropHelper(Window).Handle.ToInt64()},{"audit",Audit()}};
        if(Client!=null) {
            status["file"]=Get(Get(Client,"CurrentFile"),"FileName");
            status["hasUnsavedChanges"]=Get(Client,"HasUnsavedChanges");
            var diagram=Get(Client,"CurrentDiagram");status["page"]=Get(diagram,"Name");
            var editor=Get(Client,"DiagramEditor");status["editorType"]=editor==null?null:editor.GetType().FullName;
        }
        return status;
    }
    static void Capture(string path) {
        var target=Window;
        if(target==null&&Application.Current!=null) target=Application.Current.Windows.Cast<Window>().OrderByDescending(w=>w.ActualWidth*w.ActualHeight).FirstOrDefault();
        if(target==null || target.ActualWidth<1) throw new InvalidOperationException("No rendered RP8 window yet");
        target.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(target.ActualWidth),(int)Math.Ceiling(target.ActualHeight),96,96,PixelFormats.Pbgra32);
        bitmap.Render(target);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        using(var stream=File.Create(path)) encoder.Save(stream);
    }
    static IEnumerable<DependencyObject> VisualDescendants(DependencyObject parent) {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) {
            var child=VisualTreeHelper.GetChild(parent,i);yield return child;
            foreach(var nested in VisualDescendants(child))yield return nested;
        }
    }
    static bool InPageTree(DependencyObject child) {
        for(var parent=VisualTreeHelper.GetParent(child);parent!=null;parent=VisualTreeHelper.GetParent(parent))
            if(parent.GetType().Name.IndexOf("TreeView",StringComparison.OrdinalIgnoreCase)>=0)return true;
        return false;
    }
    static object Execute(Dictionary<string,object> request) {
        string op=Text(request,"operation","status");
        if(op=="status") return Status();
        if(op=="capture") {string path=Path.GetFullPath(Text(request,"path"));Capture(path);return new{path=path};}
        if(Client==null) throw new InvalidOperationException("RP8 client not ready");
        if(op=="methods") {
            var editor=Get(Client,"DiagramEditor");
            return new {editorType=editor==null?null:editor.GetType().FullName,
                methods=editor==null?null:editor.GetType().GetMethods(Any).Select(m=>new{token=m.MetadataToken,signature=m.ToString()}).ToArray()};
        }
        if(op=="page.open") {
            var id=Guid.Parse(Text(request,"id"));
            Client.GetType().GetMethod("OpenOuterObject").Invoke(Client,new object[]{id,Guid.Empty,false});return Status();
        }
        if(op=="page.reveal") {
            OwnFile();string name=Text(request,"name");Window.UpdateLayout();
            var label=VisualDescendants(Window).OfType<System.Windows.Controls.TextBlock>()
                .FirstOrDefault(t=>t.Text==name&&InPageTree(t));
            if(label==null) {
                // Labels below the fold may not be realized. Scroll only the native left pane.
                var scroll=VisualDescendants(Window).OfType<System.Windows.Controls.ScrollViewer>().FirstOrDefault(s=>{
                    var point=s.TransformToAncestor(Window).Transform(new Point(0,0));
                    return s.IsVisible&&point.X<270&&point.Y>=100&&point.Y<340&&s.ActualHeight<300&&s.ScrollableHeight>0;
                });
                if(scroll==null)throw new ArgumentException("Page label is not realized and no scrollable sitemap pane was found: "+name);
                scroll.ScrollToBottom();Window.UpdateLayout();return new{name=name,scrolledToEnd=true};
            }
            label.BringIntoView();Window.UpdateLayout();return new{name=name,revealed=true};
        }
        if(op=="pages") {
            var doc=Get(Client,"Document");
            return Get(doc,"DepthFirstPagePackageIdList");
        }
        if(op=="page.create") {
            OwnFile();string name=Text(request,"name");if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("A page name is required");
            QueueCurrentPageSave();Client.GetType().GetMethod("CommitOuterObject").Invoke(Client,new object[]{true});
            var doc=Get(Client,"Document");var map=Get(doc,"Sitemap");
            foreach(var existing in (IEnumerable)Get(doc,"DepthFirstPagePackageIdList")) {
                var info=Platform.ManifestModule.ResolveMethod(100672022).Invoke(doc,new[]{existing});
                if(info!=null&&Convert.ToString(Get(info,"PackageName"))==name)throw new ArgumentException("Page name already exists");
            }
            // Same constructor and package registration used by native PageTreeView.df.
            var page=Activator.CreateInstance(Model.GetType("mc"),new[]{doc,Get(Client,"CacheService")});
            var id=(Guid)Get(page,"RootPackageId");
            var packageInfo=Activator.CreateInstance(Platform.GetType("Axure.Platform.ObjectPersistanceContext.PackageInfo"),new[]{doc});
            Platform.ManifestModule.ResolveMethod(100664656).Invoke(packageInfo,new object[]{id});
            Platform.ManifestModule.ResolveMethod(100664658).Invoke(packageInfo,new object[]{"Axure:Page"});
            Platform.ManifestModule.ResolveMethod(100664660).Invoke(packageInfo,new object[]{name});
            Platform.ManifestModule.ResolveMethod(100672026).Invoke(doc,new[]{packageInfo});
            var file=Get(Client,"CurrentFile");
            file.GetType().GetMethods(Any).Single(m=>m.ReturnType==typeof(void)&&m.GetParameters().Length==2&&
                m.GetParameters()[0].ParameterType.FullName=="Axure.Platform.ObjectPersistanceContext.PackageObject"&&m.GetParameters()[1].ParameterType==typeof(Stream))
                .Invoke(file,new object[]{page,null});
            var root=request.ContainsKey("folder")?
                Model.ManifestModule.ResolveMethod(100664475).Invoke(map,new object[]{Text(request,"folder"),true,null}):
                Model.ManifestModule.ResolveMethod(100664392).Invoke(map,null);
            int count=((IEnumerable)Model.ManifestModule.ResolveMethod(100668291).Invoke(root,null)).Cast<object>().Count();
            Model.ManifestModule.ResolveMethod(100664412).Invoke(map,new object[]{null,root,count,id});
            QueuePackageSave(page);QueuePackageSave(Get(map,"Owner"));Client.GetType().GetMethod("TouchDocument").Invoke(Client,null);
            Client.GetType().GetMethod("OpenOuterObject").Invoke(Client,new object[]{id,Guid.Empty,false});
            return new{id=id,name=name,status=Status()};
        }
        if(op=="page.rename") {
            OwnFile();var map=Get(Get(Client,"Document"),"Sitemap");var id=Guid.Parse(Text(request,"id"));string name=Text(request,"name");
            if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("A page name is required");
            var nodes=(IDictionary)Model.ManifestModule.ResolveMethod(100664426).Invoke(null,new[]{map});
            if(!nodes.Contains(id))throw new ArgumentException("Unknown page");
            Model.ManifestModule.ResolveMethod(100664403).Invoke(map,new object[]{nodes[id],name});
            QueuePackageSave(Get(map,"Owner"));Client.GetType().GetMethod("TouchDocument").Invoke(Client,null);return new{id=id,name=name};
        }
        if(op=="page.removeEmpty") {
            OwnFile();var id=Guid.Parse(Text(request,"id"));var previous=Get(Get(Client,"CurrentDiagram"),"PageFragment");
            var previousId=(Guid)Get(previous,"RootPackageId");if(id==previousId)throw new ArgumentException("Switch away from the empty page first");
            var map=Get(Get(Client,"Document"),"Sitemap");var nodes=(IDictionary)Model.ManifestModule.ResolveMethod(100664426).Invoke(null,new[]{map});
            if(!nodes.Contains(id))throw new ArgumentException("Unknown page");
            Client.GetType().GetMethod("OpenOuterObject").Invoke(Client,new object[]{id,Guid.Empty,false});
            bool empty=CurrentWidgets().Count==0;
            Client.GetType().GetMethod("OpenOuterObject").Invoke(Client,new object[]{previousId,Guid.Empty,false});
            if(!empty)throw new InvalidOperationException("Refuse to remove a nonempty page");
            var children=((IEnumerable)Model.ManifestModule.ResolveMethod(100668291).Invoke(nodes[id],null)).Cast<object>().ToArray();
            if(children.Length!=0)throw new InvalidOperationException("Refuse to remove a parent page");
            Model.ManifestModule.ResolveMethod(100664414).Invoke(map,new[]{nodes[id]});
            QueuePackageSave(Get(map,"Owner"));Client.GetType().GetMethod("TouchDocument").Invoke(Client,null);return new{removed=id};
        }
        if(op=="widgets") return CurrentWidgets().Select(WidgetInfo).ToArray();
        if(op=="widget.group") {
            OwnFile();string name=Text(request,"name");var ids=((IEnumerable)request["ids"]).Cast<object>().Select(Convert.ToString).ToArray();
            if(string.IsNullOrWhiteSpace(name)||ids.Length<2||ids.Length>20||ids.Distinct().Count()!=ids.Length)throw new ArgumentException("Supply a group name and 2..20 distinct top-level widget IDs");
            if(AllWidgets().Any(w=>Convert.ToString(Get(w,"Name"))==name))throw new ArgumentException("Group name already exists");
            var members=ids.Select(id=>CurrentWidgets().Single(w=>Convert.ToString(Get(w,"UniqueIdentifier"))==id)).ToList();
            string sourceId=Text(request,"interactionSource");var source=members.Single(w=>Convert.ToString(Get(w,"UniqueIdentifier"))==sourceId);
            var selection=Array.CreateInstance(Model.GetType("ex"),members.Count);for(int i=0;i<members.Count;i++)selection.SetValue(members[i],i);
            var editor=Get(Client,"DiagramEditor");
            ClientAssembly.ManifestModule.ResolveMethod(100665391).Invoke(editor,new object[]{selection,null,false,false});
            // The original Group command builds the native Layer and its native grouping edit.
            ClientAssembly.ManifestModule.ResolveMethod(100665341).Invoke(editor,null);
            var group=CurrentWidgets().Single(w=>w.GetType().FullName=="Axure.DocumentModel.Objects.Layer"&&
                ((IEnumerable)Get(w,"DiagramObjects")).Cast<object>().Select(c=>Convert.ToString(Get(c,"UniqueIdentifier"))).OrderBy(s=>s).SequenceEqual(ids.OrderBy(s=>s)));
            Set(group,"Name",name);
            var result=RP8Interactions.Consolidate(group,source,members);
            QueueCurrentPageSave();Client.GetType().GetMethod("TouchDocument").Invoke(Client,null);
            return new{group=WidgetInfo(group),interactions=result};
        }
        if(op=="widget.style") {
            OwnFile();string id=Text(request,"id");var widget=AllWidgets().Single(w=>Convert.ToString(Get(w,"UniqueIdentifier"))==id);
            RP8Styling.Apply(widget,(Dictionary<string,object>)request["style"]);
            Client.GetType().GetMethod("TouchDocument").Invoke(Client,null);return WidgetInfo(widget);
        }
        if(op=="widget.path") {
            OwnFile();string id=Text(request,"id");var widget=AllWidgets().Single(w=>Convert.ToString(Get(w,"UniqueIdentifier"))==id);
            if(widget.GetType().FullName!="Axure.DocumentModel.Objects.VectorShape")throw new ArgumentException("A native vector is required");
            RP8Styling.VectorPath(widget,request);Client.GetType().GetMethod("TouchDocument").Invoke(Client,null);return WidgetInfo(widget);
        }
        if(op=="widget.select") {
            string id=Text(request,"id");var widget=AllWidgets().Single(w=>Convert.ToString(Get(w,"UniqueIdentifier"))==id);
            ClientAssembly.ManifestModule.ResolveMethod(100665390).Invoke(Get(Client,"DiagramEditor"),new object[]{widget,null,false,false});
            return WidgetInfo(widget);
        }
        if(op=="interaction.repairText") {
            OwnFile();string prefix=Text(request,"prefix");
            if(string.IsNullOrEmpty(prefix))throw new ArgumentException("An explicit widget name prefix is required");
            var widgets=AllWidgets().Where(w=>Convert.ToString(Get(w,"Name")).StartsWith(prefix,StringComparison.Ordinal)).ToList();
            var result=RP8Interactions.RepairPlainText(Get(Get(Client,"CurrentDiagram"),"PageFragment"),widgets);
            Client.GetType().GetMethod("TouchDocument").Invoke(Client,null);return result;
        }
        if(op=="interactions"||op=="interaction.add"||op=="interaction.replaceActions") {
            var widgets=AllWidgets();string id=Text(request,"id");
            var widget=widgets.Single(w=>Convert.ToString(Get(w,"UniqueIdentifier"))==id);
            if(op=="interactions")return RP8Interactions.Describe(widget);
            if(op=="interaction.replaceActions") {
                OwnFile();var changed=RP8Interactions.ReplaceActions(Get(Get(Client,"CurrentDiagram"),"PageFragment"),widget,request,widgets);
                QueueCurrentPageSave();Client.GetType().GetMethod("TouchDocument").Invoke(Client,null);return changed;
            }
            OwnFile();var result=RP8Interactions.AddCase(Get(Get(Client,"CurrentDiagram"),"PageFragment"),widget,request,widgets);
            Client.GetType().GetMethod("TouchDocument").Invoke(Client,null);return result;
        }
        if(op=="prototype.export") {
            OwnFile();QueueCurrentPageSave();Client.GetType().GetMethod("CommitOuterObject").Invoke(Client,new object[]{true});
            string path=Path.GetFullPath(Text(request,"path"));
            if(Directory.Exists(path)||File.Exists(path))throw new IOException("Use a new prototype directory");
            Directory.CreateDirectory(path);
            var config=Activator.CreateInstance(Model.GetType("gy"),new[]{Get(Client,"Document")});
            Model.ManifestModule.ResolveMethod(100669178).Invoke(config,new object[]{path});
            Model.ManifestModule.ResolveMethod(100669184).Invoke(config,new object[]{false});
            Model.ManifestModule.ResolveMethod(100669189).Invoke(config,new object[]{true});
            var generators=Assembly.LoadFrom(Path.Combine(Root,"Generators.dll"));
            var type=generators.GetType("Axure.Generators.Generators.HtmlPrototypeGenerator");
            var generator=Activator.CreateInstance(type,new[]{config});
            bool finished=false;
            EventHandler beforeBrowser=delegate {finished=true;Set(generator,"CancelRequested",true);};
            // Files have been generated at this event. Suppress browser launch and foreground changes.
            type.GetEvent("BeforeOpenBrowser").AddEventHandler(generator,beforeBrowser);
            type.GetMethod("Generate").Invoke(generator,new object[]{Get(Client,"CurrentFile"),null});
            if(!finished||!File.Exists(Path.Combine(path,"index.html")))throw new IOException("Native HTML export did not complete");
            return new{path=path,nativeGenerator=true,browserOpened=false,files=Directory.GetFiles(path,"*.html").Select(Path.GetFileName).ToArray()};
        }
        if(op=="frame.removeOld") {
            OwnFile();var old=CurrentWidgets().Single(w=>Convert.ToString(Get(w,"UniqueIdentifier"))==Text(request,"id"));
            var children=Descendants(old).ToArray();var r=Get(old,"Rect");
            if(old.GetType().FullName!="Axure.DocumentModel.Objects.Layer"||Convert.ToString(Get(old,"Name"))!="原刘海边框（隐藏）"||children.Length!=14||children.Skip(1).Any(w=>w.GetType().Name!="ImageBox")||Convert.ToDouble(Get(r,"Width"))!=415||Convert.ToDouble(Get(r,"Height"))!=851)
                throw new ArgumentException("Only the identified 13-part old notch frame may be removed");
            foreach(var item in children)if(((IEnumerable)RP8Interactions.Describe(item)).Cast<object>().Any(e=>((IEnumerable)Get(e,"cases")).Cast<object>().Any()))throw new InvalidOperationException("Refuse frame with interactions");
            var array=Array.CreateInstance(Model.GetType("ex"),children.Length);for(int i=0;i<children.Length;i++)array.SetValue(children[children.Length-1-i],i);
            Model.ManifestModule.ResolveMethod(100663858).Invoke(Get(Get(Client,"CurrentDiagram"),"DiagramObjects"),new object[]{array});
            QueueCurrentPageSave();Client.GetType().GetMethod("TouchDocument").Invoke(Client,null);return new{removed=children.Length};
        }
        if(op=="page.content")return RP8Interactions.Content(AllWidgets());
        if(op=="page.polish") {
            OwnFile();var widgets=AllWidgets();var names=(IDictionary<string,object>)request["names"];
            if(names.Keys.Any(id=>!widgets.Any(w=>Convert.ToString(Get(w,"UniqueIdentifier"))==id)))throw new ArgumentException("Unknown widget ID");
            if(names.Values.Any(v=>string.IsNullOrWhiteSpace(Convert.ToString(v))))throw new ArgumentException("Empty widget name");
            var replacements=(IDictionary<string,object>)request["textReplacements"];
            var cases=(IDictionary<string,object>)request["caseNames"];
            var parent=Get(Get(Client,"CurrentDiagram"),"PageFragment");
            var result=RP8Interactions.PolishContent(parent,widgets,replacements,cases);int texts=0;
            foreach(var widget in widgets) {
                object name;if(names.TryGetValue(Convert.ToString(Get(widget,"UniqueIdentifier")),out name))Set(widget,"Name",Convert.ToString(name));
                if(widget.GetType().GetProperty("Text",Any)!=null) {
                    object text;if(replacements.TryGetValue(Convert.ToString(Get(widget,"Text")),out text)){Set(widget,"Text",Convert.ToString(text));texts++;}
                }
            }
            Client.GetType().GetMethod("TouchDocument").Invoke(Client,null);QueueCurrentPageSave();
            return new{names=names.Count,texts=texts,interactions=result};
        }
        if(op=="widget.create") return CreateWidget(request);
        if(op=="widget.update") {
            OwnFile();string id=Text(request,"id");var widget=AllWidgets().Single(w=>Convert.ToString(Get(w,"UniqueIdentifier"))==id);
            if(request.ContainsKey("text"))Set(widget,"Text",Text(request,"text"));
            if(request.ContainsKey("name"))Set(widget,"Name",Text(request,"name"));
            if(request.ContainsKey("placeholder"))Set(widget,"PlaceholderText",Text(request,"placeholder"));
            if(request.ContainsKey("showBorder"))Set(widget,"ShowBorder",Convert.ToBoolean(request["showBorder"]));
            if(request.ContainsKey("hideHintOnFocus"))Set(widget,"HideHintOnFocus",Convert.ToBoolean(request["hideHintOnFocus"]));
            if(request.ContainsKey("placeholderColor"))RP8Styling.HintColor(widget,Text(request,"placeholderColor"));
            Bounds(widget,request);Client.GetType().GetMethod("TouchDocument").Invoke(Client,null);
            return WidgetInfo(widget);
        }
        if(op=="undo"||op=="redo") {
            OwnFile();Client.GetType().GetMethod(op=="undo"?"Undo":"Redo",Type.EmptyTypes).Invoke(Client,null);return Status();
        }
        if(op=="save") {
            OwnFile();QueueCurrentPageSave();Client.GetType().GetMethod("CommitOuterObject").Invoke(Client,new object[]{true});
            var file=Get(Client,"CurrentFile");string current=Convert.ToString(Get(file,"FileName"));
            string path=Path.GetFullPath(Text(request,"path",current));
            if(!path.EndsWith(".rp",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Output must be .rp");
            if(!string.Equals(current,path,StringComparison.OrdinalIgnoreCase)&&File.Exists(path))throw new IOException("Output already exists");
            if(string.Equals(current,path,StringComparison.OrdinalIgnoreCase)) {
                // Native normal-save path: writes storage, clears dirty packages and emits FileSaved.
                file.GetType().GetMethods(Any).Single(m=>m.Name=="c"&&m.ReturnType==typeof(void)&&m.GetParameters().Length==0).Invoke(file,null);
            } else {
                // Native Save As updates the file location and runs the same normal-save path.
                var method=file.GetType().GetMethods(Any).Single(m=>m.Name=="c"&&m.ReturnType==typeof(bool)&&m.GetParameters().Length==2&&m.GetParameters()[0].ParameterType==typeof(string));
                object[] parameters={path,null};
                if(!Convert.ToBoolean(method.Invoke(file,parameters)))throw new IOException(Convert.ToString(parameters[1]));
            }
            OwnedFiles.Add(path);return new{path=path,sha256=Hash(path),status=Status()};
        }
        if(op=="close") {
            if(Convert.ToBoolean(Get(Client,"HasUnsavedChanges")))throw new InvalidOperationException("Save before closing this instance");
            Window.Dispatcher.BeginInvoke(new Action(()=>Client.GetType().GetMethod("CloseClient").Invoke(Client,null)),DispatcherPriority.ApplicationIdle);
            return new{closing=true};
        }
        throw new ArgumentException("Unsupported live operation: "+op);
    }
    static void Tick(object sender,EventArgs args) {
        try {
            if(Client==null) Client=FindClient();
            if(Client!=null&&Window==null) Window=Get(Client,"Form") as Window;
            if(Window!=null&&!Configured) {
                Window.ShowActivated=false;Window.Topmost=false;Window.WindowState=WindowState.Normal;
                Window.Width=1150;Window.Height=820;Window.Left=20;Window.Top=30;
                Configured=true;
            }
            if(Window!=null&&!Window.Title.StartsWith("[AI RP8] ")) Window.Title="[AI RP8] "+Window.Title;
            Write(Path.Combine(Home,"session.json"),Status());
            foreach(string path in Directory.GetFiles(Requests,"*.request.json").OrderBy(p=>p).Take(1)) {
                string processing=path.Replace(".request.json",".processing.json");File.Move(path,processing);
                var result=new Dictionary<string,object>{{"before",Audit()},{"processId",Pid}};
                try {var request=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(processing,Encoding.UTF8));result["result"]=Execute(request);result["ok"]=true;}
                catch(Exception e) {while(e is TargetInvocationException&&e.InnerException!=null)e=e.InnerException;result["ok"]=false;result["error"]=e.ToString();}
                result["after"]=Audit();Write(path.Replace(".request.json",".result.json"),result);
            }
        } catch(Exception e) {Write(Path.Combine(Session,"tick-error.json"),new{error=e.ToString()});}
    }
    [STAThread] static int Main(string[] args) {
        Root=AppDomain.CurrentDomain.BaseDirectory;
        Home=Path.Combine(Root,"rp8-automation","live");
        bool created;InstanceMutex=new System.Threading.Mutex(true,"Local\\Codex-RP8Live-8-3400",out created);
        if(!created){InstanceMutex.Dispose();return 0;}
        if(args.Length!=1||!File.Exists(args[0]))return 2;
        OwnedFiles.Add(Path.GetFullPath(args[0]));
        Session=Path.Combine(Home,"sessions",Guid.NewGuid().ToString("N"));Requests=Path.Combine(Session,"requests");
        Directory.CreateDirectory(Requests);
        Write(Path.Combine(Home,"session.json"),new{processId=Pid,ready=false,sessionDirectory=Session,audit=Audit()});
        try {
            var expected=new Dictionary<string,string>{{"AxureRP8.exe","DDDE5C4EC3281F4C0522A38DD38E7498B0D70FFDF126E7F8916C212A109FD279"},
                {"Client.dll","4AD07217D548C6F9E5E17AAAA1C55E627B0F4F1855E203A946D88881B0CCF109"},
                {"Client.Win32.dll","9D7F4FAA0B8C3C02CFE125A32F4A123B2CF2F91EDC080074C715EE63A7215074"},
                {"Model.dll","58B45B074F502EBE4B345346BDF8AC8680F7A9A4A8C53693922CE4AA2D4B3C65"},
                {"Platform.dll","D53B20B6658932BEF477E592FAAC5A028EF3B8BF482B77F0E7E8C9D1E2A83A87"},
                {"Platform.Win32.dll","3C59611656496F8BF00DB641C4D92E5BC172461288BE2BF570440363486F12D6"}};
            foreach(var p in expected)if(Hash(Path.Combine(Root,p.Key))!=p.Value)throw new InvalidOperationException("Unsupported DLL: "+p.Key);
            Callback=Guard;Hook=SetWindowsHookEx(5,Callback,IntPtr.Zero,GetCurrentThreadId());
            if(Hook==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Cannot protect focus");
            Environment.CurrentDirectory=Root;
            AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){var path=Path.Combine(Root,new AssemblyName(e.Name).Name+".dll");return File.Exists(path)?Assembly.LoadFrom(path):null;};
            Timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(150)};Timer.Tick+=Tick;Timer.Start();
            var exe=Assembly.LoadFrom(Path.Combine(Root,"AxureRP8.exe"));
            Model=Assembly.LoadFrom(Path.Combine(Root,"Model.dll"));Platform=Assembly.LoadFrom(Path.Combine(Root,"Platform.dll"));ClientAssembly=Assembly.LoadFrom(Path.Combine(Root,"Client.dll"));
            exe.EntryPoint.Invoke(null,new object[]{args});
            return 0;
        } catch(Exception e) {
            while(e is TargetInvocationException&&e.InnerException!=null)e=e.InnerException;
            Write(Path.Combine(Session,"startup-error.json"),new{error=e.ToString()});return 1;
        } finally {if(Hook!=IntPtr.Zero)UnhookWindowsHookEx(Hook);if(InstanceMutex!=null){InstanceMutex.ReleaseMutex();InstanceMutex.Dispose();}}
    }
}
