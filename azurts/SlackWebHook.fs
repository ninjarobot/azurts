﻿module azurts.SlackWebHook

open System
open System.Net.Http
open System.Text
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
                do! Json.write "type" "section"
                do! Json.write "text" text
            }
        | Fields (fields) ->
            json {
                do! Json.write "type" "section"
                do! Json.write "fields" fields
            }
type Context =
    | Elements of Text list
    static member ToJson (c: Context) =
        match c with
        | Elements (elements) ->
            json {
                do! Json.write "type" "context"
                do! Json.write "elements" elements
            }
type Divider =
    | Divider
    static member ToJson (d: Divider) =
        match d with
        | Divider ->
            json {
                do! Json.write "type" "divider"
            }

type Block =
    | Context of Context
    | Divider of Divider
    | Section of Section
    static member ToJson (b:Block) =
        match b with
        | Context c -> Context.ToJson c
        | Divider d -> Divider.ToJson d
        | Section s -> Section.ToJson s

type Payload =
    {
        Channel : string
        Blocks : Block list
    }
    static member ToJson (p:Payload) =
        json {
            do! Json.write "channel" p.Channel
            do! Json.write "blocks" p.Blocks
        }
type SlackWebHookConfig =
    {
        Client : HttpClient
        Token : string option
        WebHookUri : System.Uri
        ErrorCallback : System.Net.HttpStatusCode * string -> unit
    }
    
module Payload =
    let format (payload:Payload) =
        payload |> Json.serialize |> Json.formatWith JsonFormattingOptions.Pretty
    
    /// Azure Alert severity: 0 = critical, 1 = error, 2 = warning, 3 = informational, 4 = verbose
    let severityIcon (severity:int) =
        if severity = 0 then ":bomb:"
        elif severity = 1 then ":fire:"
        elif severity = 2 then ":warning:"
        else ":information_source:"
    
    let private toTitleCase = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase
    
    let dataFromJson (json:Chiron.Json) =
        match json with
        | Chiron.Json.String s ->
            let len = s.Length
            // Max length for Slack section text is 3000.
            if len > 2900 then // Grab the first 2900 leaving a little for formatting. Alert payloads already may be truncated.
                s.Substring (0, 2900)
            else
                s
        | Chiron.Json.Number n -> string n
        | Chiron.Json.Bool b -> string b
        | Chiron.Json.Null _ -> String.Empty
        | json -> Json.format json
    
    let ofAzureAlert (channel:string) (alert:AzureAlert.LogAlert) : Payload seq option =
        let heading = String.Format ("{0} *{1}*", alert.Data.Severity |> severityIcon, alert.Data.AlertRuleName)
        let alertTimeRange = String.Format ("Between <!date^{0}^{{date_num}} {{time_secs}}|{1}> and <!date^{2}^{{date_num}} {{time_secs}}|{3}> <{4}|Search Results>",
                                            alert.Data.SearchIntervalStartTime.ToUnixTimeSeconds(),
                                            alert.Data.SearchIntervalStartTime.ToString(),
                                            alert.Data.SearchIntervalEndTime.ToUnixTimeSeconds(),
                                            alert.Data.SearchIntervalEndTime.ToString(),
                                            alert.Data.LinkToSearchResults)
        alert.Data.SearchResult.Tables |> List.tryHead |> Option.map
            (
            fun table ->
                seq {
                    for row in table.Rows do
                        let item = Seq.zip table.Columns row
                        let fields =
                            item
                            |> Seq.filter (fun (column, _) -> column.Name <> "message")
                            |> Seq.filter (fun (column, row) -> match row with | Json.Null _ -> false | _ -> true )
                            |> Seq.map (fun (column, row) -> LabeledText(column.Name.Replace("customDimensions_", ""), dataFromJson row ))
                        let message = item |> Seq.tryFind (fun (column, _) -> column.Name = "message") |> Option.map (fun (_, row) -> dataFromJson row)
                        let payload =
                            {
                                Channel = channel
                                Blocks =
                                    [
                                        yield Section (Text (Markdown (heading)))
                                        if not (String.IsNullOrEmpty alert.Data.Description) then
                                            yield Section (Text (Markdown (alert.Data.Description)))
                                        yield Section (Fields (fields |> List.ofSeq))
                                        if message.IsSome then
                                            yield Section (Text (Markdown (String.Format ("```{0}```", message.Value))))
                                        yield Context (Elements [ Markdown (alertTimeRange) ])
                                    ]
                            }
                        yield payload
                }
            )
    
    let sendToSlack (config:SlackWebHookConfig) (payloads:Payload seq) =
        async {
            for payload in payloads do
                use content = new StringContent (payload |> Json.serialize |> Json.format, Encoding.UTF8, "application/json")
                use request = new HttpRequestMessage (HttpMethod.Post, config.WebHookUri, Content=content)
                config.Token |> Option.iter (fun token ->
                    request.Headers.Authorization <- System.Net.Http.Headers.AuthenticationHeaderValue ("Bearer", token)
                )
                use! response = request |> config.Client.SendAsync |> Async.AwaitTask
                if not response.IsSuccessStatusCode then
                    let! responseBody = response.Content.ReadAsStringAsync () |> Async.AwaitTask
                    config.ErrorCallback (response.StatusCode, responseBody)
        } |> Some
