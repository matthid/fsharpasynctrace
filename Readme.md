
### FSharp Async Tracing

This Library allows you to trace you async workflows in FSharp. 


### Using

just look into the Script.fsx file for simple usage.
It's almost as simple as using async directly.

Just open fsharpAsyncTrace and fsharpAsyncTrace.AsyncTrace (name it as you like)
and replace "async" with "asyncTrace()" (note the ()).

Now you can get a tracer within the {} expression via
let! (tracer:YOURFAVORITETRACEOBJECT) = AsyncTrace.traceInfo()

But be carefull, right before running your "asyncTrace" you have to actually assign your tracer.
To execute it just convert it to an Async with "convertToAsync".

It is _really_ simple, just go ahaid and look into the Script.fsx file!

### License 

see License.md (MIT-Lizense)

