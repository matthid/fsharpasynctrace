// Weitere Informationen zu F# unter "http://fsharp.net".
namespace fsharpAsyncTrace

type ITracer = 
    abstract member log : System.Diagnostics.TraceEventType ->Printf.StringFormat<'a, unit> -> 'a

/// Allows tracing of async workflows
module AsyncTrace = 
    type IAsyncTrace<'Info> = 
        abstract member Info : 'Info option with get,set

        abstract member Capture : System.Collections.Generic.List<IAsyncTrace<'Info>> -> unit

    type TraceList<'Info> = System.Collections.Generic.List<IAsyncTrace<'Info>>
    type AsyncTrace<'Info, 'T>(info:'Info option, execute:Async<'Info option * 'T>) = 
        let mutable info = info
        let mutable list = new TraceList<'Info>() 
        interface IAsyncTrace<'Info> with
            member x.Info 
                with get() = info
                and set(newValue) = info <- newValue 
            member x.Capture (newList:TraceList<'Info>) =
                // TODO: This has to be done properly
                if not (newList.Contains(x)) then newList.Add(x)
                for current in list do
                    if not (newList.Contains(current)) then 
                        newList.Add(current)
                        current.Capture newList
                                    
                list <- newList

                let dataItems = 
                    list 
                        |> Seq.filter (fun f -> f.Info.IsSome)
                            
                let mutable data = None
                for dataItem in dataItems do
                    let info = dataItem.Info
                    if (info.IsSome) then
                        if (data.IsSome && not (obj.ReferenceEquals(data.Value, info.Value))) then 
                            failwith "multiple different data!"
                        data <- info
                if (data.IsSome) then
                    for dataItem in list |> Seq.filter (fun f -> f.Info.IsNone) do
                        dataItem.Info <- Some data.Value

        member internal x.Async = execute
        member x.SetInfo value = info <- Some value
    

    type AsyncTraceBuilder<'Info, 'T>() as x = 
        let internalList = new TraceList<'Info>()

        let buildTrace async = 
            let b = new AsyncTrace<'Info, 'Y>(None, async)
            (b :> IAsyncTrace<'Info>).Capture internalList
            b
            

        let bind (value:AsyncTrace<'Info, 'X>) (f:'X -> AsyncTrace<'Info, 'Y>) =
            (value :> IAsyncTrace<'Info>).Capture internalList
            
            buildTrace 
                (async.Bind(
                    value.Async, 
                    (fun (info, t) -> 
                        let inner = f(t)
                        (inner:>IAsyncTrace<'Info>).Info <- (value:>IAsyncTrace<'Info>).Info
                        inner.Async)))

        let delay (f:unit -> AsyncTrace<'Info, 'Y>) = 
            buildTrace (async.Delay (fun () -> (f()).Async))

        let returnN t = 
            buildTrace (
                async {
                    let! d = async.Return(t)
                    return None, d
                })

        let returnFrom (t:AsyncTrace<'Info, _>) = 
            (t:> IAsyncTrace<'Info>).Capture internalList
            buildTrace (async.ReturnFrom(t.Async))

        let combine (item1:AsyncTrace<'Info, unit>) (item2:AsyncTrace<'Info, _>) = 
            (item1:> IAsyncTrace<'Info>).Capture internalList
            (item2:> IAsyncTrace<'Info>).Capture internalList
            buildTrace (
                async.Combine(
                    async {
                        let! d = item1.Async
                        
                        return()
                    }, item2.Async))

        let forComp (sequence:seq<'X>) (work:'X -> AsyncTrace<'Info, unit>) = 
            buildTrace (
                async { 
                    let! t = 
                        async.For
                            (sequence, 
                            (fun t -> 
                                async {
                                    do! (work t).Async |> Async.Ignore
                                    return ()
                                })) 
                    return None, t } )

        let tryFinally (item:AsyncTrace<'Info,_>) f = 
            (item:> IAsyncTrace<'Info>).Capture internalList
            buildTrace (async.TryFinally(item.Async, f))
        
        let tryWith (item: AsyncTrace<'Info,_>) (exnHandler:exn -> AsyncTrace<'Info,_>) = 
            (item:> IAsyncTrace<'Info>).Capture internalList
            buildTrace (async.TryWith(item.Async, (fun xn -> (exnHandler xn).Async)))
        
        let usingComp (item) (doWork:_-> AsyncTrace<'Info,_>) = 
            buildTrace (async.Using(item, (fun t -> (doWork t).Async)))

        let whileComp (item) (work: AsyncTrace<'Info,_>) = 
            (work:> IAsyncTrace<'Info>).Capture internalList
            buildTrace 
                (async {
                    do! (async.While(
                            item, 
                            async {
                                do! work.Async |> Async.Ignore
                                return ()
                            }))
                    return None, ()
                })

        let zeroComp () = 
            buildTrace 
                (async {
                    let! t = async.Zero()
                    return None, t
                })


        // AsyncTrace<'Info, 'T> * ('T -> AsyncTrace<'Info, 'U>) -> AsyncTrace<'Info, 'U>
        member x.Bind(value, f) = bind value f
        // (unit -> AsyncTrace<'Info,'T>) -> AsyncTrace<'Info,'T> 
        member x.Delay(f) = delay f
        member x.Return(t) = returnN t
        member x.ReturnFrom(t) =  returnFrom t
        member x.Combine(t1,t2) = combine t1 t2
        member x.For(t1,t2) = forComp t1 t2
        member x.TryFinally(t1,t2) = tryFinally t1 t2
        member x.TryWith(t1,t2) = tryWith t1 t2
        member x.Using(t1,t2) = usingComp t1 t2
        member x.While(t1,t2) = whileComp t1 t2
        member x.Zero() = zeroComp ()


    let asyncTrace() = new AsyncTraceBuilder<_,_>()
    
    let traceInfo () : AsyncTrace<'a,_> =
        asyncTrace() {
            let b = (asyncTrace() {return()})
            let! a = b // Small hack to get connected
            return 
                match (b:>IAsyncTrace<_>).Info with
                | None -> 
                    failwith "Please set the info value via builder"
                | Some v -> v : 'a 
        } 

    let convertFromAsync asy = 
        new AsyncTrace<_,_>(None, 
            async {
                let! d = asy
                return None, d
            })
    let convertToAsync (traceAsy:AsyncTrace<_,_>) = 
        async {
            let! info,d = traceAsy.Async
            return d
        }

    let Ignore (traceAsy:AsyncTrace<_,_>) =
        asyncTrace() {
            let! item = traceAsy
            return ()
        }

    type ITracer with
        member x.logVerb fmt = x.log System.Diagnostics.TraceEventType.Verbose fmt
        member x.logWarn fmt = x.log System.Diagnostics.TraceEventType.Warning fmt
        member x.logCrit fmt = x.log System.Diagnostics.TraceEventType.Critical fmt
        member x.logErr fmt =  x.log System.Diagnostics.TraceEventType.Error fmt
        member x.logInfo fmt = x.log System.Diagnostics.TraceEventType.Information fmt     

    type DefaultStateTracer(state:string) = 
        let logHelper ty (s : string) = printfn "%s-%s: %s" state (ty.ToString()) s
        interface ITracer with 
            member x.log ty fmt = Printf.kprintf (logHelper ty) fmt  

