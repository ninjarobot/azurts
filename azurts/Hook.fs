module azurts.Hook

type Hook<'a, 'b> = 'a -> 'b option

let compose (first: 'a -> 'b option) (second: 'b -> 'c option) : 'a -> 'c option =
    fun input -> input |> first |> Option.bind second

let (>=>) = compose
