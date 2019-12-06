module azurts.Hook

type Hook<'a, 'b> = 'a -> 'b option

let compose (first: 'a -> 'b option) (second: 'b -> 'c option) : 'a -> 'c option =
    fun input -> input |> first |> Option.bind second

let (>=>) = compose

/// Sends copy of the input to multiple hooks.
let broadcast (hooks: Hook<'a, Async<unit>> list) =
    fun input ->
        hooks
        |> List.map (fun hook -> input |> hook)
        |> List.choose id
        |> function
        | [] -> None
        | filteredHooks -> filteredHooks |> Async.Parallel |> Async.Ignore |> Some
