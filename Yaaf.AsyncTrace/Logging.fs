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
        abstract member log : Diagnostics.TraceEventType ->Printf.StringFormat<'a, unit> -> 'a
        abstract member TraceSource : TraceSource

[<AutoOpen>]
module LoggingModule = 
    type MyTraceSource(traceEntry:string,name:string) as x= 
        inherit TraceSource(traceEntry)
        do 
            let newTracers = [|
                for l in x.Listeners do
                    let t = l.GetType()
                    let initField =
                        t.GetField(
                            "initializeData", System.Reflection.BindingFlags.NonPublic ||| 
                                                System.Reflection.BindingFlags.Instance)
                    let oldRelFilePath =
                        if initField <> null then
                                initField.GetValue(l) :?> string
                        else System.IO.Path.Combine("logs", sprintf "%s.log" l.Name)
                    
                    let newFileName =
                        if oldRelFilePath = "" then ""
                        else
                            let fileName = Path.GetFileNameWithoutExtension(oldRelFilePath)
                            let extension = Path.GetExtension(oldRelFilePath)
                            Path.Combine(
                                Path.GetDirectoryName(oldRelFilePath),
                                sprintf "%s.%s%s" fileName name extension)
                    let constr = t.GetConstructor(if newFileName = "" then [| |] else [| typeof<string> |])
                    if (constr = null) then 
                        failwith (sprintf "TraceListener Constructor for Type %s not found" (t.FullName))
                    let listener = constr.Invoke(if newFileName = "" then [| |]  else [| newFileName |]) :?> TraceListener
                    yield listener |]
            x.Listeners.Clear()
            x.Listeners.AddRange(newTracers)

    type DefaultStateTracer(traceSource:TraceSource, activityName:string) = 
        let trace = traceSource
        let activity = Guid.NewGuid()
        let doInId f = 
            let oldId = Trace.CorrelationManager.ActivityId
            try
                Trace.CorrelationManager.ActivityId <- activity
                f()
            finally
                Trace.CorrelationManager.ActivityId <- oldId
        let logHelper ty (s : string) =  
            doInId 
                (fun () ->
                    trace.TraceEvent(ty, 0, s)
                    trace.Flush())
        do 
            doInId (fun () -> trace.TraceEvent(TraceEventType.Start, 0, activityName))
        
        member x.ActivityId = activity
        interface IDisposable with
            member x.Dispose() = 
                doInId (fun () -> trace.TraceEvent(TraceEventType.Stop, 0, activityName))
        
        interface ITracer with 
            member x.log ty fmt = Printf.kprintf (logHelper ty) fmt  
            member x.TraceSource = trace

    module Logging = 
        let MySource traceEntry name = new MyTraceSource(traceEntry, name)
        let Source entryName = new TraceSource(entryName)

        let DefaultTracer traceSource id = 
            new DefaultStateTracer(traceSource, id) :> ITracer
        
        let SetDefaultTracer traceSource id (traceAsy:AsyncTrace<_,_>) = 
            traceAsy |> AsyncTrace.SetTracer (DefaultTracer traceSource id)
        
    type ITracer with 
        member x.logVerb fmt = x.log System.Diagnostics.TraceEventType.Verbose fmt
        member x.logWarn fmt = x.log System.Diagnostics.TraceEventType.Warning fmt
        member x.logCrit fmt = x.log System.Diagnostics.TraceEventType.Critical fmt
        member x.logErr fmt =  x.log System.Diagnostics.TraceEventType.Error fmt
        member x.logInfo fmt = x.log System.Diagnostics.TraceEventType.Information fmt
        member x.childTracer baseTracer newActivity = 
            let tracer = new DefaultStateTracer(baseTracer, newActivity)
            x.log System.Diagnostics.TraceEventType.Transfer "%s" (tracer.ActivityId.ToString())
            tracer :> ITracer
