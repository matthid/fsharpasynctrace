// Diese Datei ist ein Skript, das mit F# interaktiv ausgeführt werden kann.  
// Es kann zur Erkundung und zum Testen des Bibliotheksprojekts verwendet werden.
// Skriptdateien gehören nicht zum Projektbuild.

#load "fsharpAsyncTrace.fs"
open fsharpAsyncTrace
open fsharpAsyncTrace.AsyncTrace

let doSomeThingInner v = asyncTrace() {
    let! (tracer:ITracer) = AsyncTrace.traceInfo()
    tracer.logCrit "CRITICAL! %s" v
    return "ToOuter" }

let testIt () = asyncTrace() {
    let! (tracer:ITracer) = AsyncTrace.traceInfo()
    tracer.logVerb "Verbose!" 
    let! d = doSomeThingInner "ToInner"
    tracer.logWarn "WARNING: %s" d }
    
// Using it right => Set the tracer before invoking
let workFlow = testIt()
workFlow.SetInfo (new DefaultStateTracer("Workflow 01"))
workFlow |> convertToAsync |> Async.RunSynchronously

// WRONG!! No tracer set! => Exception
testIt() |> convertToAsync |> Async.RunSynchronously