using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

// Adapter for the hash-bound RP8 8.0.0.3400 native interaction tree.
internal static class RP8Interactions {
    const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static Assembly Model {get{return AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Model");}}
    static object Native(int token,object target,params object[] args) {return Model.ManifestModule.ResolveMethod(token).Invoke(target,args);}
    static object New(string name,params object[] args) {return Activator.CreateInstance(Model.GetType(name),args);}
    static object Get(object o,string name) {
        var p=o.GetType().GetProperty(name,Any);
        return p!=null?p.GetValue(o,null):o.GetType().GetMethods(Any).Single(m=>m.Name=="get_"+name&&m.GetParameters().Length==0).Invoke(o,null);
    }
    static void Set(object o,string name,object value) {o.GetType().GetProperty(name,Any).GetSetMethod(true).Invoke(o,new[]{value});}
    static string Text(IDictionary<string,object> d,string key,string fallback="") {object v;return d.TryGetValue(key,out v)&&v!=null?Convert.ToString(v):fallback;}
    static void Call(object o,string name,params object[] args) {o.GetType().GetMethods(Any).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(o,args);}
    static object EnumValue(string type,string value) {return Enum.Parse(Model.GetType(type),value,false);}
    static string Numeric(IDictionary<string,object> d,string key,double fallback) {
        object v;double n=d.TryGetValue(key,out v)?Convert.ToDouble(v,CultureInfo.InvariantCulture):fallback;
        if(double.IsNaN(n)||double.IsInfinity(n)||Math.Abs(n)>20000)throw new ArgumentException("Invalid "+key);
        return n.ToString(CultureInfo.InvariantCulture);
    }
    static string GeometryValue(IDictionary<string,object> d,string key,double fallback) {
        string value=Text(d,key);
        if(value.StartsWith("[[")&&value.EndsWith("]]")) {
            if(value.Length>1500)throw new ArgumentException("Geometry expression is too long");
            return value;
        }
        return Numeric(d,key,fallback);
    }
    static object ExpressionLocals(object parent,IDictionary<string,object> spec,IList<object> widgets) {
        var bindings=spec.ContainsKey("sources")?spec["sources"] as IDictionary<string,object>:null;
        if(bindings==null) {
            string source=Text(spec,"source");if(source=="")return null;
            bindings=new Dictionary<string,object>{{"LVAR1",source}};
        }
        if(bindings.Count<1||bindings.Count>4)throw new ArgumentException("Supply 1..4 local widget sources");
        var locals=New("cm");
        foreach(var binding in bindings) {
            if(binding.Key.Length!=5||!binding.Key.StartsWith("LVAR")||binding.Key[4]<'1'||binding.Key[4]>'4')throw new ArgumentException("Local names must be LVAR1..LVAR4");
            var source=widgets.Single(w=>Convert.ToString(Get(w,"UniqueIdentifier"))==Convert.ToString(binding.Value));
            var args=Array.CreateInstance(Model.GetType("Axure.DocumentModel.Expressions.Expr"),1);
            args.SetValue(New("Axure.DocumentModel.Expressions.DiagramObjectPathLiteral",parent,PathTo(parent,source)),0);
            Native(100668607,locals,New("b7",binding.Key,New("Axure.DocumentModel.Expressions.FCall",parent,"GetWidgetText",args)));
        }
        return locals;
    }
    static object PathTo(object parent,object widget) {
        return New("nd",parent,new List<Guid>(),new List<Guid>(),Guid.Parse(Convert.ToString(Get(widget,"UniqueIdentifier"))));
    }
    static object Condition(object parent,IDictionary<string,object> spec,IList<object> widgets) {
        string id=Text(spec,"target"),kind=Text(spec,"kind");
        if(kind!="textEquals"&&kind!="checked")throw new ArgumentException("Unsupported condition: "+kind);
        var target=widgets.Single(w=>Convert.ToString(Get(w,"UniqueIdentifier"))==id);
        var literal=New("Axure.DocumentModel.Expressions.DiagramObjectPathLiteral",parent,PathTo(parent,target));
        var args=Array.CreateInstance(Model.GetType("Axure.DocumentModel.Expressions.Expr"),1);args.SetValue(literal,0);
        var left=New("Axure.DocumentModel.Expressions.FCall",parent,kind=="checked"?"GetCheckState":"GetWidgetText",args);
        object right=kind=="checked"?New("Axure.DocumentModel.Expressions.BooleanLiteral",parent,Convert.ToBoolean(spec["value"])):
            New("Axure.DocumentModel.Expressions.StringLiteral",parent,Text(spec,"text"),null,null,null);
        var platform=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Platform");
        string comparison=Text(spec,"comparison","Equals");
        if(comparison!="Equals"&&comparison!="NotEquals")throw new ArgumentException("Unsupported comparison");
        return New("Axure.DocumentModel.Expressions.BinaryExpr",parent,Enum.Parse(platform.GetType("Axure.Platform.LogicalComparison"),comparison),left,right);
    }
    public static object Describe(object widget) {
        var map=Native(100664240,widget);
        if(map==null)return new object[0];
        return ((IEnumerable)Native(100664318,map)).Cast<object>().Select(e=>new {
            eventType=Convert.ToString(Get(e,"EventType")),
            cases=((IEnumerable)e.GetType().GetMethod("GetCases").Invoke(e,null)).Cast<object>().Select(c=>new {
                id=Convert.ToString(Get(c,"UniqueIdentifier")),name=Get(c,"Description"),
                actions=((IEnumerable)c.GetType().GetMethod("GetActions").Invoke(c,null)).Cast<object>().Select(a=>new {
                    id=Convert.ToString(Get(a,"UniqueIdentifier")),type=a.GetType().Name,text=Get(a,"EditorText")
                }).ToArray()
            }).ToArray()
        }).ToArray();
    }
    public static object Consolidate(object group,object source,IList<object> children) {
        group.GetType().GetMethod("UpdateIxMap").Invoke(group,null);
        var destination=Native(100664240,group);var original=Native(100664240,source);
        // Native map merge clones the preferred component's cases and their expressions.
        Native(100664348,destination,original);
        int removed=0;
        foreach(var child in children) {
            var map=Native(100664240,child);
            var cases=((IEnumerable)Native(100664326,map)).Cast<object>().ToArray();
            foreach(var caze in cases) {
                var node=Native(100664400,map,caze);Native(100664414,map,node);removed++;
            }
            child.GetType().GetMethod("UpdateIxMap").Invoke(child,null);
        }
        group.GetType().GetMethod("UpdateIxMap").Invoke(group,null);
        return new{removedChildCases=removed,group=Describe(group),children=children.Select(w=>new{name=Get(w,"Name"),events=Describe(w)}).ToArray()};
    }
    static object Action(object parent,IDictionary<string,object> spec,IList<object> widgets,object map) {
        string kind=Text(spec,"kind");
        if(kind=="openPage") {
            var link=New("Axure.DocumentModel.Interaction.IxLinkWindowAction",parent,EnumValue("Axure.DocumentModel.Interaction.LinkType","Current"));
            var linkTarget=Get(link,"Target");
            Set(linkTarget,"TargetType",EnumValue("Axure.DocumentModel.Interaction.IxTargetType","Page"));
            Set(linkTarget,"TargetPagePackageId",Guid.Parse(Text(spec,"page")));return link;
        }
        if(kind=="wait") {
            int ms=int.Parse(Numeric(spec,"milliseconds",200),CultureInfo.InvariantCulture);
            if(ms<0||ms>10000)throw new ArgumentException("wait must be 0..10000 ms");
            var wait=New("Axure.DocumentModel.Interaction.IxWaitAction",parent);Set(wait,"WaitTime",ms);return wait;
        }
        string id=Text(spec,"target");
        var target=widgets.Single(w=>Convert.ToString(Get(w,"UniqueIdentifier"))==id);
        var path=PathTo(parent,target);
        if(kind=="fire") {
            string eventName=Text(spec,"event","OnClick");
            var eventType=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Core").GetType("Axure.Constants.IxEventType");
            var eventValue=Enum.Parse(eventType,eventName);
            if(!((IEnumerable)Get(target,"SupportedIxEventTypes")).Cast<object>().Contains(eventValue))throw new ArgumentException("Target does not support "+eventName);
            var fire=New("Axure.DocumentModel.Interaction.IxFireEventsAction",parent,false);
            Call(fire,"AddPath",path);
            fire.GetType().GetMethods(Any).Single(m=>m.Name=="AddFiredEvent"&&m.GetParameters().Length==2&&m.GetParameters()[1].ParameterType==eventType)
                .Invoke(fire,new[]{path,eventValue});return fire;
        }
        if(kind=="visibility") {
            var fade=EnumValue("Axure.DocumentModel.Interaction.FadeActionType",Text(spec,"mode","Toggle"));
            var a=New("Axure.DocumentModel.Interaction.IxFadeWidgetAction",parent,fade,new Dictionary<string,double>());
            Call(a,"AddPath",path);Call(a,"SetDuration",path,0d);Call(a,"SetDurationHide",path,0d);return a;
        }
        if(kind=="enable") {
            var a=New("Axure.DocumentModel.Interaction.IxEnableDisableWidgetsAction",parent,Convert.ToBoolean(spec["enabled"]));
            Call(a,"AddPath",path);return a;
        }
        if(kind=="move"||kind=="resize") {
            var a=New("Axure.DocumentModel.Interaction."+(kind=="move"?"IxMoveWidgetAction":"IxSetWidgetSizeAction"),parent,new Dictionary<string,double>());
            Set(a,"IxMap",map);
            Call(a,"AddPath",path);
            if(kind=="move") {
                Call(a,"SetXValue",path,GeometryValue(spec,"x",0),ExpressionLocals(parent,spec,widgets));Call(a,"SetYValue",path,GeometryValue(spec,"y",0),ExpressionLocals(parent,spec,widgets));
                Call(a,"SetMoveType",path,EnumValue("Axure.DocumentModel.Expressions.MoveBehavior",Text(spec,"mode","Delta")));
            } else {
                string width=GeometryValue(spec,"width",220),height=GeometryValue(spec,"height",44);
                if((!width.StartsWith("[[")&&double.Parse(width,CultureInfo.InvariantCulture)<1)||(!height.StartsWith("[[")&&double.Parse(height,CultureInfo.InvariantCulture)<1))throw new ArgumentException("Positive dimensions required");
                Call(a,"SetWidth",path,width,ExpressionLocals(parent,spec,widgets));Call(a,"SetHeight",path,height,ExpressionLocals(parent,spec,widgets));
            }
            Call(a,"SetDuration",path,0d);return a;
        }
        if(kind=="setText"||kind=="setChecked"||kind=="copyText"||kind=="expressionText") {
            bool form=target.GetType().Name=="TextBox"||target.GetType().Name=="Button";
            string fn=kind=="setChecked"?"SetCheckState":form?"SetWidgetFormText":"SetWidgetRichText";
            var a=New("Axure.DocumentModel.Interaction.IxActionSetFunction",parent,fn);
            var targetLiteral=New("Axure.DocumentModel.Expressions.DiagramObjectPathLiteral",parent,path);
            // setText takes plain text. Pair a string literal with the explicit native
            // plain=true argument; RichTextLiteral would serialize <p>/<span> markup.
            var value=New("Axure.DocumentModel.Expressions.StringLiteral",parent,kind=="setChecked"?Text(spec,"value","toggle"):Text(spec,"text"),null,null,null);
            if(kind=="copyText") {
                var source=widgets.Single(w=>Convert.ToString(Get(w,"UniqueIdentifier"))==Text(spec,"source"));
                var sourceArgs=Array.CreateInstance(Model.GetType("Axure.DocumentModel.Expressions.Expr"),1);
                sourceArgs.SetValue(New("Axure.DocumentModel.Expressions.DiagramObjectPathLiteral",parent,PathTo(parent,source)),0);
                value=New("Axure.DocumentModel.Expressions.FCall",parent,"GetWidgetText",sourceArgs);
            }
            if(kind=="expressionText") {
                string expression=Text(spec,"expression");
                if(expression.Length>1500||!expression.StartsWith("[[")||!expression.EndsWith("]]"))throw new ArgumentException("A bounded native [[expression]] is required");
                var locals=ExpressionLocals(parent,spec,widgets);
                value=New("Axure.DocumentModel.Expressions.StringLiteral",parent,expression,locals,null,null);
            }
            var args=Array.CreateInstance(Model.GetType("Axure.DocumentModel.Expressions.Expr"),fn=="SetWidgetRichText"?3:2);
            args.SetValue(targetLiteral,0);args.SetValue(value,1);
            if(args.Length==3)args.SetValue(New("Axure.DocumentModel.Expressions.BooleanLiteral",parent,true),2);
            var fcall=New("Axure.DocumentModel.Expressions.FCall",parent,fn,args);
            Call(Get(a,"Action"),"AddSubExpression",fcall);return a;
        }
        throw new ArgumentException("Unsupported interaction action: "+kind);
    }
    public static object RepairPlainText(object parent,IList<object> widgets) {
        var changed=new List<object>();
        foreach(var widget in widgets) {
            var map=Native(100664240,widget);if(map==null)continue;
            foreach(var ev in ((IEnumerable)Native(100664318,map)).Cast<object>())
            foreach(var caze in ((IEnumerable)ev.GetType().GetMethod("GetCases").Invoke(ev,null)).Cast<object>())
            foreach(var action in ((IEnumerable)caze.GetType().GetMethod("GetActions").Invoke(caze,null)).Cast<object>()) {
                if(action.GetType().Name!="IxActionSetFunction"||Convert.ToString(Get(action,"SetFunction"))!="SetWidgetRichText")continue;
                foreach(var expression in ((IEnumerable)Get(Get(action,"Action"),"SubExpressions")).Cast<object>()) {
                    if(expression.GetType().Name!="FCall"||Convert.ToString(Get(expression,"FunctionName"))!="SetWidgetRichText")continue;
                    var old=((IEnumerable)Get(expression,"Args")).Cast<object>().ToArray();
                    if(old.Length<2)continue;
                    string type=old[1].GetType().Name;
                    if(type!="RichTextLiteral"&&type!="StringLiteral")continue;
                    if(type=="StringLiteral"&&old.Length==3&&old[2].GetType().Name=="BooleanLiteral"&&Convert.ToBoolean(Get(old[2],"LiteralValue")))continue;
                    string text=Convert.ToString(Get(old[1],"LiteralTextValue"));
                    var args=Array.CreateInstance(Model.GetType("Axure.DocumentModel.Expressions.Expr"),3);
                    args.SetValue(old[0],0);args.SetValue(New("Axure.DocumentModel.Expressions.StringLiteral",parent,text,null,null,null),1);
                    args.SetValue(New("Axure.DocumentModel.Expressions.BooleanLiteral",parent,true),2);Set(expression,"Args",args);
                    changed.Add(new{widget=Get(widget,"Name"),caseId=Get(caze,"UniqueIdentifier"),actionId=Get(action,"UniqueIdentifier"),text=text});
                }
            }
            widget.GetType().GetMethod("UpdateIxMap").Invoke(widget,null);
        }
        return new{count=changed.Count,actions=changed.ToArray()};
    }
    static IEnumerable<object> PageCases(object widget) {
        var map=Native(100664240,widget);if(map==null)yield break;
        foreach(var caze in ((IEnumerable)Native(100664326,map)).Cast<object>())yield return caze;
    }
    static IEnumerable<object> ActionLiterals(object expression) {
        if(expression==null)yield break;
        if(expression.GetType().Name=="StringLiteral")yield return expression;
        foreach(string property in new[]{"SubExpressions","Args"}) {
            var prop=expression.GetType().GetProperty(property,Any);if(prop==null)continue;
            var children=prop.GetValue(expression,null) as IEnumerable;if(children==null)continue;
            foreach(var child in children)foreach(var literal in ActionLiterals(child))yield return literal;
        }
    }
    public static object Content(IList<object> widgets) {
        return widgets.Select(w=>new {
            id=Get(w,"UniqueIdentifier"),name=Get(w,"Name"),
            text=w.GetType().GetProperty("Text",Any)==null?null:Get(w,"Text"),
            cases=PageCases(w).Select(c=>new {
                id=Get(c,"UniqueIdentifier"),name=Get(c,"Description"),
                literals=((IEnumerable)c.GetType().GetMethod("GetActions").Invoke(c,null)).Cast<object>()
                    .Where(a=>a.GetType().Name=="IxActionSetFunction")
                    .SelectMany(a=>ActionLiterals(Get(a,"Action"))).Select(l=>Convert.ToString(Get(l,"LiteralTextValue"))).ToArray()
            }).ToArray()
        }).ToArray();
    }
    static int ReplaceLiteralArgs(object parent,object expression,IDictionary<string,object> replacements) {
        int count=0;
        foreach(string property in new[]{"SubExpressions","Args"}) {
            var prop=expression.GetType().GetProperty(property,Any);if(prop==null)continue;
            var children=prop.GetValue(expression,null) as IEnumerable;if(children==null)continue;
            var values=children.Cast<object>().ToArray();bool changed=false;
            for(int i=0;i<values.Length;i++) {
                var item=values[i];object replacement;
                if(item.GetType().Name=="StringLiteral"&&replacements.TryGetValue(Convert.ToString(Get(item,"LiteralTextValue")),out replacement)) {
                    string old=Convert.ToString(Get(item,"LiteralTextValue"));
                    if(old.Contains("[["))throw new ArgumentException("Do not replace expression literals");
                    values[i]=New("Axure.DocumentModel.Expressions.StringLiteral",parent,Convert.ToString(replacement),null,null,null);changed=true;count++;
                } else count+=ReplaceLiteralArgs(parent,item,replacements);
            }
            if(changed) {
                if(property!="Args")throw new InvalidOperationException("Expected native function argument");
                var array=Array.CreateInstance(Model.GetType("Axure.DocumentModel.Expressions.Expr"),values.Length);
                for(int i=0;i<values.Length;i++)array.SetValue(values[i],i);Set(expression,property,array);
            }
        }
        return count;
    }
    public static object PolishContent(object parent,IList<object> widgets,IDictionary<string,object> replacements,IDictionary<string,object> caseNames) {
        int changed=0,cases=0;
        foreach(var widget in widgets) {
            foreach(var caze in PageCases(widget)) {
                object name;if(caseNames.TryGetValue(Convert.ToString(Get(caze,"UniqueIdentifier")),out name)){Set(caze,"Description",Convert.ToString(name));cases++;}
                foreach(var action in ((IEnumerable)caze.GetType().GetMethod("GetActions").Invoke(caze,null)).Cast<object>())
                    if(action.GetType().Name=="IxActionSetFunction")changed+=ReplaceLiteralArgs(parent,Get(action,"Action"),replacements);
            }
            widget.GetType().GetMethod("UpdateIxMap").Invoke(widget,null);
        }
        return new{literalChanges=changed,caseNameChanges=cases};
    }
    public static object AddCase(object parent,object widget,IDictionary<string,object> request,IList<object> widgets) {
        string eventName=Text(request,"event","OnClick"),name=Text(request,"name","AI交互用例");
        var eventType=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Core").GetType("Axure.Constants.IxEventType");
        if(!Enum.IsDefined(eventType,eventName))throw new ArgumentException("Unknown native event: "+eventName);
        object eventValue=Enum.Parse(eventType,eventName);
        if(!((IEnumerable)Get(widget,"SupportedIxEventTypes")).Cast<object>().Contains(eventValue))throw new ArgumentException("Widget does not support "+eventName);
        var specs=request["actions"] as IList;
        if(specs==null||specs.Count<1||specs.Count>20)throw new ArgumentException("Supply 1..20 actions");
        var map=Native(100664240,widget);
        var existing=((IEnumerable)Native(100664318,map)).Cast<object>().Where(e=>Equals(Get(e,"EventType"),eventValue)).ToArray();
        if(existing.SelectMany(e=>((IEnumerable)e.GetType().GetMethod("GetCases").Invoke(e,null)).Cast<object>()).Any(c=>Convert.ToString(Get(c,"Description"))==name))
            throw new ArgumentException("A case with that name already exists in this event");
        // Construct all actions before publishing the new case into the native tree.
        var actions=specs.Cast<IDictionary<string,object>>().Select(s=>Action(parent,s,widgets,map)).ToArray();
        object condition=request.ContainsKey("condition")?Condition(parent,(IDictionary<string,object>)request["condition"],widgets):null;
        object eventNode=Native(100664329,map,eventValue);
        if(eventNode==null) {
            var ev=New("Axure.DocumentModel.Interaction.IxEvent",parent,eventValue);
            eventNode=Native(100664412,map,null,Native(100664392,map),0,ev);
        }
        var caze=New("Axure.DocumentModel.Interaction.IxCase",parent);Set(caze,"Description",name);
        if(condition!=null)Set(caze,"Condition",condition);
        int count=((IEnumerable)Native(100668291,eventNode)).Cast<object>().Count();
        var node=Native(100664412,map,null,eventNode,count,caze);
        for(int i=0;i<actions.Length;i++)Native(100664412,map,null,node,i,actions[i]);
        widget.GetType().GetMethod("UpdateIxMap").Invoke(widget,null);
        return new {id=Convert.ToString(Get(caze,"UniqueIdentifier")),eventType=eventName,name=name,interactions=Describe(widget)};
    }
    public static object ReplaceActions(object parent,object widget,IDictionary<string,object> request,IList<object> widgets) {
        var map=Native(100664240,widget);string id=Text(request,"caseId");
        var caze=((IEnumerable)Native(100664326,map)).Cast<object>().Single(c=>Convert.ToString(Get(c,"UniqueIdentifier"))==id);
        var specs=request["actions"] as IList;if(specs==null||specs.Count<1||specs.Count>20)throw new ArgumentException("Supply 1..20 actions");
        var actions=specs.Cast<IDictionary<string,object>>().Select(s=>Action(parent,s,widgets,map)).ToArray();
        var node=Native(100664400,map,caze);
        var old=((IEnumerable)caze.GetType().GetMethod("GetActions").Invoke(caze,null)).Cast<object>().ToArray();
        foreach(var action in old)Native(100664414,map,Native(100664400,map,action));
        for(int i=0;i<actions.Length;i++)Native(100664412,map,null,node,i,actions[i]);
        widget.GetType().GetMethod("UpdateIxMap").Invoke(widget,null);
        return new{caseId=id,oldActionCount=old.Length,newActionCount=actions.Length,interactions=Describe(widget)};
    }
}
