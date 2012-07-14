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

type IMyListener = 
    abstract member Duplicate : string -> TraceListener

module CopyListenerHelper = 
    let copyListener (fromListener:TraceListener) (toListener:TraceListener) = 
        toListener.Attributes.Clear()
        for pair in 
            fromListener.Attributes.Keys
                |> Seq.cast
                |> Seq.map2 (fun k v -> k,v) (fromListener.Attributes.Values |> Seq.cast) do
            toListener.Attributes.Add pair
        toListener.Filter <- fromListener.Filter
        toListener.IndentLevel <- fromListener.IndentLevel
        toListener.Name <- fromListener.Name
        toListener.TraceOutputOptions <- fromListener.TraceOutputOptions
        toListener

    let createNewFilename oldRelFilePath name =
        if System.String.IsNullOrEmpty(oldRelFilePath) then null
        else
            let fileName = Path.GetFileNameWithoutExtension(oldRelFilePath)
            let extension = Path.GetExtension(oldRelFilePath)
            Path.Combine(
                Path.GetDirectoryName(oldRelFilePath),
                sprintf "%s.%s%s" fileName name extension)

type MyDefaultTraceListener(initData:string, name:string) = 
    inherit DefaultTraceListener()
    new() = new MyDefaultTraceListener(null,null)
    new(s) = new MyDefaultTraceListener(s,s)
    interface IMyListener with
        member x.Duplicate name =
            CopyListenerHelper.copyListener
                x
                (new DefaultTraceListener() :> TraceListener)

type MyXmlWriterTraceListener(initData:string, name:string) = 
    inherit XmlWriterTraceListener(initData)
    new(s) = new MyXmlWriterTraceListener(s,s)
    interface IMyListener with
        member x.Duplicate name =
            let newPath = CopyListenerHelper.createNewFilename initData name
            CopyListenerHelper.copyListener 
                x
                (new MyXmlWriterTraceListener(newPath) :> TraceListener)

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
            let flags = System.Reflection.BindingFlags.Public |||
                        System.Reflection.BindingFlags.NonPublic ||| 
                        System.Reflection.BindingFlags.Instance
            let invalid = Path.GetInvalidFileNameChars() 
                                |> Seq.append (Path.GetInvalidPathChars())   
            let cleanPath = 
                (
                name 
                    |> Seq.fold 
                        (fun (builder:Text.StringBuilder) char -> 
                            builder.Append(
                                if invalid |> Seq.exists (fun i -> i = char) 
                                then '_'
                                else char))
                        (new System.Text.StringBuilder(name.Length))
                ).ToString()

            let eventCache = new TraceEventCache(); 

            let newTracers = [|
                for l in x.Listeners do
                    match l:>obj with
                    | :? IMyListener as myListener ->
                        yield myListener.Duplicate(cleanPath)
                    | _ ->
                        l.TraceEvent(eventCache, x.Name, TraceEventType.Error, 0, sprintf "Unknown Listener, can't apply name \"%s\"" name)
                |]
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