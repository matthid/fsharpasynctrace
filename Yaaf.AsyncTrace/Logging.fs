// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.AsyncTrace

open System
open System.Diagnostics
open System.IO

[<AutoOpen>]
module LogInterface =
    type ITracer = 
        inherit IDisposable
        abstract member TraceSource : TraceSource
        abstract member ActivityId : Guid

[<AutoOpen>]
module LoggingModule = 
    type ITracer with 
        member x.doInId f = 
            let oldId = Trace.CorrelationManager.ActivityId
            try
                Trace.CorrelationManager.ActivityId <- x.ActivityId
                f()
            finally
                Trace.CorrelationManager.ActivityId <- oldId
        member x.logHelper ty (s : string) =  
            x.doInId 
                (fun () ->
                    x.TraceSource.TraceEvent(ty, 0, s)
                    x.TraceSource.Flush())
        member x.log ty fmt = Printf.kprintf (x.logHelper ty) fmt  
        member x.logVerb fmt = x.log System.Diagnostics.TraceEventType.Verbose fmt
        member x.logWarn fmt = x.log System.Diagnostics.TraceEventType.Warning fmt
        member x.logCrit fmt = x.log System.Diagnostics.TraceEventType.Critical fmt
        member x.logErr fmt =  x.log System.Diagnostics.TraceEventType.Error fmt
        member x.logInfo fmt = x.log System.Diagnostics.TraceEventType.Information fmt

    type MyTraceSource(traceEntry:string,name:string) as x= 
        inherit TraceSource(traceEntry)
        do 
            // NOTE: Shared Listeners are not supported
            // (currently we create new ones and do not share them the same way)
            let flags = System.Reflection.BindingFlags.NonPublic ||| 
                                                System.Reflection.BindingFlags.Instance
            let newTracers = [|
                for l in x.Listeners do
                    let t = l.GetType()
                    let initField =
                        t.GetField(
                            "initializeData", flags)
                    let oldRelFilePath =
                        if initField <> null then
                                initField.GetValue(l) :?> string
                        else System.IO.Path.Combine("logs", sprintf "%s.log" l.Name)
                    
                    let newFileName =
                        if System.String.IsNullOrEmpty(oldRelFilePath) then null
                        else
                            let fileName = Path.GetFileNameWithoutExtension(oldRelFilePath)
                            let extension = Path.GetExtension(oldRelFilePath)
                            Path.Combine(
                                Path.GetDirectoryName(oldRelFilePath),
                                sprintf "%s.%s%s" fileName name extension)

                    // NOTE: On mono the DefaultTraceSource Listener Constructor is private
                    let constr = 
                        t.GetConstructor(
                            flags, 
                            null, 
                            (if newFileName = null then [| |] else [| typeof<string> |]), 
                            null)
                    if (constr = null) then 
                        failwith (sprintf "TraceListener Constructor for Type %s not found" (t.FullName))
                    let listener = constr.Invoke(if newFileName = null then [| |]  else [| newFileName |]) :?> TraceListener
                    // Copy other properties.
                    listener.Attributes.Clear()
                    for pair in 
                        l.Attributes.Keys
                            |> Seq.cast
                            |> Seq.map2 (fun k v -> k,v) (l.Attributes.Values |> Seq.cast) do
                        listener.Attributes.Add pair
                    listener.Filter <- l.Filter
                    listener.IndentLevel <- l.IndentLevel
                    listener.Name <- l.Name
                    listener.TraceOutputOptions <- l.TraceOutputOptions
                    yield listener |]
            x.Listeners.Clear()
            x.Listeners.AddRange(newTracers)

    type DefaultStateTracer(traceSource:TraceSource, activityName:string) as x= 
        let trace = traceSource
        let activity = Guid.NewGuid()
        let self = x :> ITracer
        do 
            x.doInId (fun () -> trace.TraceEvent(TraceEventType.Start, 0, activityName))
        
        interface IDisposable with
            member x.Dispose() = 
                x.doInId (fun () -> trace.TraceEvent(TraceEventType.Stop, 0, activityName))
        
        interface ITracer with 
            member x.TraceSource = trace
            member x.ActivityId = activity

    module Logging = 
        let MySource traceEntry name = new MyTraceSource(traceEntry, name)
        let Source entryName = new TraceSource(entryName)

        let DefaultTracer traceSource id = 
            new DefaultStateTracer(traceSource, id) :> ITracer
        
        let SetDefaultTracer traceSource id (traceAsy:AsyncTrace<_,_>) = 
            traceAsy |> AsyncTrace.SetTracer (DefaultTracer traceSource id)
       
        
    type ITracer with 
        member x.childTracer baseTracer newActivity = 
            let tracer = new DefaultStateTracer(baseTracer, newActivity) :> ITracer
            x.doInId 
                (fun () -> 
                    x.TraceSource.TraceTransfer(0, "Switching to " + newActivity, tracer.ActivityId))
            tracer