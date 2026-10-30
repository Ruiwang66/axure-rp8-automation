using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Media;

// Whitelisted native appearance properties and editable vector paths.
internal static class RP8Styling {
    const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static Assembly Platform {get{return AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Platform");}}
    static Assembly Model {get{return AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Model");}}
    static object Get(object o,string p) {return o.GetType().GetProperty(p,Any).GetValue(o,null);}
    static void Set(object o,string p,object v) {o.GetType().GetProperty(p,Any).GetSetMethod(true).Invoke(o,new[]{v});}
    static object Color(string text) {
        text=text.TrimStart('#');if(text.Length==6)text="FF"+text;
        if(text.Length!=8)throw new ArgumentException("Color must be RRGGBB or AARRGGBB");
        return Platform.ManifestModule.ResolveMethod(100670787).Invoke(null,new object[]{Convert.ToUInt32(text,16)});
    }
    static object Fill(object spec) {
        var gradient=spec as IDictionary<string,object>;
        if(gradient==null)return Activator.CreateInstance(Platform.GetType("Axure.Platform.Drawing.SolidFill"),new[]{Color(Convert.ToString(spec))});
        bool hasMiddle=gradient.ContainsKey("middle");
        var stops=Array.CreateInstance(Platform.GetType("Axure.Platform.Drawing.GradientStop"),hasMiddle?3:2);
        stops.SetValue(Activator.CreateInstance(stops.GetType().GetElementType(),new object[]{0d,Color(Convert.ToString(gradient["from"]))}),0);
        if(hasMiddle) {
            double offset=gradient.ContainsKey("middleOffset")?Convert.ToDouble(gradient["middleOffset"]):.5d;
            if(double.IsNaN(offset)||offset<=0||offset>=1)throw new ArgumentException("Gradient middleOffset must be between 0 and 1");
            stops.SetValue(Activator.CreateInstance(stops.GetType().GetElementType(),new object[]{offset,Color(Convert.ToString(gradient["middle"]))}),1);
        }
        stops.SetValue(Activator.CreateInstance(stops.GetType().GetElementType(),new object[]{1d,Color(Convert.ToString(gradient["to"]))}),hasMiddle?2:1);
        var fill=Activator.CreateInstance(Platform.GetType("Axure.Platform.Drawing.LinearGradientFill"),new object[]{stops});
        Set(fill,"FillAngle",gradient.ContainsKey("angle")?Convert.ToDouble(gradient["angle"]):0d);return fill;
    }
    public static void Apply(object widget,IDictionary<string,object> spec) {
        var list=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Platform.GetType("Axure.Platform.ObjectPersistanceContext.StyleProp")));
        var names=new Dictionary<string,string>{{"fontFamily","FONT_NAME"},{"fontSize","FONT_SIZE"},{"fontWeight","FONT_WEIGHT"},
            {"color","FORE_GROUND_FILL"},{"fill","FILL"},{"borderColor","BORDER_FILL"},{"borderWidth","BORDER_WIDTH"},
            {"radius","CORNER_RADIUS"},{"align","HORIZONTAL_ALIGNMENT"},{"valign","VERTICAL_ALIGNMENT"},
            {"paddingLeft","PADDING_LEFT"},{"paddingRight","PADDING_RIGHT"},{"paddingTop","PADDING_TOP"},{"paddingBottom","PADDING_BOTTOM"},
            {"rotation","ROTATION"},{"opacity","OPACITY"},{"visible","VISIBLE"},{"shadow","OUTER_SHADOW"}};
        var pname=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Core").GetType("Axure.Constants.PName");
        foreach(var pair in spec) {
            if(!names.ContainsKey(pair.Key))throw new ArgumentException("Unsupported appearance: "+pair.Key);
            object value;
            if(pair.Key=="fill"||pair.Key=="color"||pair.Key=="borderColor")value=Fill(pair.Value);
            else if(pair.Key=="align"||pair.Key=="valign")value=Enum.Parse(Platform.GetType("Axure.Platform.AxStringAlignment"),Convert.ToString(pair.Value),false);
            else if(pair.Key=="fontFamily")value=Convert.ToString(pair.Value);
            else if(pair.Key=="fontWeight")value=Convert.ToInt32(pair.Value);
            else if(pair.Key=="visible")value=Convert.ToBoolean(pair.Value);
            else if(pair.Key=="shadow") {
                var shadow=(IDictionary<string,object>)pair.Value;
                value=Activator.CreateInstance(Platform.GetType("Axure.Platform.Drawing.Shadow"),new object[]{true,
                    Convert.ToDouble(shadow["x"]),Convert.ToDouble(shadow["y"]),Convert.ToDouble(shadow["blur"]),0d,Color(Convert.ToString(shadow["color"]))});
            } else {
                double number=Convert.ToDouble(pair.Value);
                if(double.IsInfinity(number)||double.IsNaN(number)||Math.Abs(number)>20000)throw new ArgumentException("Invalid appearance value");
                value=number;
            }
            list.Add(Platform.ManifestModule.ResolveMethod(100672339).Invoke(null,new object[]{Enum.Parse(pname,names[pair.Key]),value,"RP8Live"}));
        }
        widget.GetType().GetMethods(Any).Single(m=>m.Name=="SetStyle"&&m.GetParameters().Length==2).Invoke(widget,new object[]{list,false});
    }
    public static void HintColor(object widget,string color) {
        var state=Enum.Parse(Platform.GetType("Axure.Platform.ObjectPersistanceContext.IxStyleState"),"Hint");
        using((IDisposable)Model.ManifestModule.ResolveMethod(100664144).Invoke(widget,new[]{state})) {
            Apply(widget,new Dictionary<string,object>{{"color",color}});
        }
    }
    public static void VectorPath(object widget,IDictionary<string,object> request) {
        string data=Convert.ToString(request["path"]);if(data.Length>30000)throw new ArgumentException("Path too large");
        double x=Convert.ToDouble(request["x"]),y=Convert.ToDouble(request["y"]),width=Convert.ToDouble(request["width"]),height=Convert.ToDouble(request["height"]);
        double viewWidth=request.ContainsKey("viewWidth")?Convert.ToDouble(request["viewWidth"]):24d;
        double viewHeight=request.ContainsKey("viewHeight")?Convert.ToDouble(request["viewHeight"]):24d;
        if(viewWidth<=0||viewHeight<=0)throw new ArgumentException("Positive vector viewport required");
        var geometry=Geometry.Parse(data).GetFlattenedPathGeometry(.05,ToleranceType.Absolute);
        var pointType=Platform.GetType("Axure.Platform.Drawing.Point");
        var bezierType=Platform.GetType("Axure.Platform.Drawing.AxBezierPoint");
        var loopType=typeof(List<>).MakeGenericType(bezierType);
        var loops=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(loopType));
        foreach(var figure in geometry.Figures) {
            var points=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(pointType));
            Action<System.Windows.Point> add=p=>points.Add(Activator.CreateInstance(pointType,new object[]{x+p.X*width/viewWidth,y+p.Y*height/viewHeight}));
            add(figure.StartPoint);
            foreach(var segment in figure.Segments) {
                var poly=segment as PolyLineSegment;if(poly!=null)foreach(var p in poly.Points)add(p);
                var line=segment as LineSegment;if(line!=null)add(line.Point);
            }
            // The native AxBexierPointList helpers expect cubic triples, not a flattened polyline.
            // Create flat native nodes explicitly; only the last node of an open path has no next segment.
            var loop=(IList)Activator.CreateInstance(loopType);
            for(int i=0;i<points.Count;i++)loop.Add(Activator.CreateInstance(bezierType,new object[]{points[i],figure.IsClosed||i<points.Count-1}));
            loops.Add(loop);
        }
        Model.ManifestModule.ResolveMethod(100670447).Invoke(widget,new object[]{loops,false});
    }
}
