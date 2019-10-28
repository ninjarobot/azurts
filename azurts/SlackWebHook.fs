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
    
    let severityIcon (severity:int) =
        if severity > 2 then ":fire:"
        elif severity = 2 then ":warning:"
        else ":information_source:"
    
    let private toTitleCase = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase
    
    let dataFromJson (json:Chiron.Json) =
        match json with
        | Chiron.Json.String s -> s
        | Chiron.Json.Number n -> string n
        | Chiron.Json.Bool b -> string b
        | Chiron.Json.Null n -> String.Empty
        | json -> Json.format json
    
    let ofAzureAlert (channel:string) (alert:AzureAlert.LogAlert) =
        let heading = String.Format ("{0} *{1}*", alert.Data.Severity |> severityIcon, alert.Data.AlertRuleName)
        let alertTimeRange = String.Format ("Between <!date^{0}^{{date_num}} {{time_secs}}> and <!date^{1}^{{date_num}} {{time_secs}}>", alert.Data.SearchIntervalStartTime.ToUnixTimeSeconds(), alert.Data.SearchIntervalEndTime.ToUnixTimeSeconds())
        alert.Data.SearchResult.Tables |> List.tryHead |> Option.map
            (
            fun table ->
                seq {
                    for row in table.Rows do
                        let item = Seq.zip table.Columns row
                        let fields =
                            item
                            |> Seq.filter (fun (column, _) -> column.Name <> "message")
                            |> Seq.map (fun (column, row) -> LabeledText(column.Name.Replace("customDimensions_", ""), dataFromJson row ))
                        let message = item |> Seq.tryFind (fun (column, _) -> column.Name = "message") |> Option.map (fun (_, row) -> dataFromJson row)
                        let payload =
                            {
                                Channel = channel
                                Blocks =
                                    [
                                        yield Section.Text (Markdown (heading))
                                        yield Section.Text (Markdown (alertTimeRange))
                                        yield Section.Text (Markdown (alert.Data.Description))
                                        yield Section.Fields (fields |> List.ofSeq)
                                        if message.IsSome then
                                            yield Section.Text (Markdown (String.Format ("```{0}```", message.Value)))
                                    ]
                            }
                        yield payload
                }
            )
