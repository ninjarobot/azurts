module azurts.SlackWebHook

open System
open Chiron

type Text =
    | Markdown of string
    | LabeledText of Label:string * Content:string
    static member ToJson (t:Text) =
        match t with
        | Markdown (text) ->
            json {
                do! Json.write "type" "mrkdwn"
                do! Json.write "text" text
            }
        | LabeledText (label, content) ->
            json {
                do! Json.write "type" "mrkdwn"
                do! Json.write "text" (String.Format ("*{0}*\n{1}", label, content))
            }
        
type Section =
    | Text of Text
    | Fields of Text list
    static member ToJson (s: Section) =
        match s with
        | Text (text:Text) ->
            json {
                do! Json.write "text" text
            }
        | Fields (fields) ->
            json {
                do! Json.write "fields" fields
            }
type Payload =
    {
        Channel : string
        Blocks : Section list
    }
    static member ToJson (p:Payload) =
        json {
            do! Json.write "channel" p.Channel
            do! Json.write "blocks" p.Blocks
        }
module Payload =
    let format (payload:Payload) =
        payload |> Json.serialize |> Json.formatWith JsonFormattingOptions.Pretty
