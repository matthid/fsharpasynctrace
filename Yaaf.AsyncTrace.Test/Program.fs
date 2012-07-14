// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

open Yaaf.AsyncTrace

let doSomeThingInner v = asyncTrace() {
    let! (tracer:ITracer) = AsyncTrace.TraceInfo()
    tracer.logCrit "CRITICAL! %s" v
    return "ToOuter" }

let testIt () = asyncTrace() {
    let! (tracer:ITracer) = traceInfo()
    tracer.logVerb "Verbose!" 
    let! d = doSomeThingInner "ToInner"
    tracer.logWarn "WARNING: %s" d }
    
// Using it right => Set the tracer before invoking
let workFlow = testIt()
// use (Logging.MySource "Yaaf.AsyncTrace" "Name") instead of (Logging.Source "Yaaf.AsyncTrace") 
// if you have the same workflow running multiple times in parallel
// you get then files added with the name 
// For example if you configure logs/logfile.xml in app.config
// you will then get multiple files with the name logs/logfile.name.xml
workFlow 
    |> AsyncTrace.SetTracer (Logging.DefaultTracer (Logging.Source "Yaaf.AsyncTrace") "Workflow 01")
    |> Async.RunSynchronously

// Using it right => Set the tracer before invoking
let workFlow2 = testIt()
// use (Logging.MySource "Yaaf.AsyncTrace" "Name") instead of (Logging.Source "Yaaf.AsyncTrace") 
// if you have the same workflow running multiple times in parallel
// you get then files added with the name 
// For example if you configure logs/logfile.xml in app.config
// you will then get multiple files with the name logs/logfile.name.xml
workFlow2
    |> AsyncTrace.SetTracer (Logging.DefaultTracer (Logging.MySource "Yaaf.AsyncTrace.Test" "Test") "Workflow 01")
    |> Async.RunSynchronously

