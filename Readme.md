
### FSharp Async Tracing

This Library allows you to trace you async workflows in F# (FSharp). 


### Using

just look into the Script.fsx file for simple usage.
It's almost as simple as using async directly.

Just 
```fsharp
open Yaaf.AsyncTrace
```

and replace "async" with "asyncTrace()" (note the ()). 
```fsharp
asyncTrace() {
    let! x = someheavycomp()
    return 0
}
```

Now you can get a tracer within the {} expression via
```fsharp
let myComp () = 
    asyncTrace() {
        let! (tracer:ITracer) = AsyncTrace.TraceInfo()
        tracer.logErr "I'm still here"
        return 3
    }
```
ITracer can be anything you like (as AsyncTrace is full generic on that)

But be carefull, right before running your "asyncTrace" you have to actually assign your tracer.
To execute it just convert it to an Async with AsyncTrace.SetTracer.
```fsharp
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
```

It is _really_ simple, just go ahaid and look into the Script.fsx file or
 go straight ahaid and use the fsharpAsyncTrace.fs in your code.

### Additional Notes

I know this is currently not done properly, 
but it took me several attempts to get it even running.

So I'm asking for only one thing: 
If you use this in your code, find it usefull and change it to a better state, please submit a patch back here.

### License 

see License.md (MIT-Lizense)

