
### FSharp Async Tracing

This Library allows you to trace you async workflows in F#. 


### Using

just look into the Script.fsx file for simple usage.
It's almost as simple as using async directly.

Just 
```fsharp
open fsharpAsyncTrace
open fsharpAsyncTrace.AsyncTrace
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
		let! (tracer:ITracer) = AsyncTrace.traceInfo()
		tracer.DoSomeLogging "I'm still here"
		return 3
	}
```
ITracer can be anything you like really (as its full generic)

But be carefull, right before running your "asyncTrace" you have to actually assign your tracer.
To execute it just convert it to an Async with "convertToAsync".
```f#
let workFlow = myComp()
// Set your tracer, remember that you can use your own types here!
workFlow.SetInfo (new DefaultStateTracer("Workflow 01") :> ITracer)
workFlow |> convertToAsync |> Async.RunSynchronously
```

It is _really_ simple, just go ahaid and look into the Script.fsx file or
 go straight ahaid and use the fsharpAsyncTrace.fs in your code.

### Additional Notes

I know this is currently not done properly, 
but it took me several attempts to get it even running.

So I'm asking for only one thing: 
If you use this in your code and find it usefull and change it to a better state, please submit a patch back here.

### License 

see License.md (MIT-Lizense)

