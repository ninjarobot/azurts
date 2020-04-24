module azurts.TeamsWebHook

open System
open System.Net.Http
open Chiron

module ContentTypes =
    [<Literal>]
    let TeamsCardO365Connector = "application/vnd.microsoft.teams.card.o365connector"

type Fact =
    {
        Name : string
        Value : string
    }
    static member ToJson(fact:Fact) =
        json {
            do! Json.write "name" fact.Name
            do! Json.write "value" fact.Value
        }

type Section =
    {
        Title : string option
        StartGroup : bool option
        ActivityImage : string option
        ActivityTitle : string option
        ActivitySubtitle : string option
        ActivityText : string option
        Text : string option
        Facts : Fact list option
    }
    static member ToJson (s: Section) =
        json {
            do! Json.writeUnlessDefault "title" None s.Title
            do! Json.writeUnlessDefault "startGroup" None s.StartGroup
            do! Json.writeUnlessDefault "activityImage" None s.ActivityImage
            do! Json.writeUnlessDefault "activityTitle" None s.ActivityTitle
            do! Json.writeUnlessDefault "activitySubtitle" None s.ActivitySubtitle
            do! Json.writeUnlessDefault "activityText" None s.ActivityText
            do! Json.writeUnlessDefault "text" None s.Text
            do! Json.writeUnlessDefault "facts" None s.Facts
        }

type OpenUriTargetOS =
    | Default
    | Windows
    | IOS
    | Android
    static member ToJson (os:OpenUriTargetOS) =
        match os with
        | Default -> "default"
        | Windows -> "windows"
        | IOS -> "iOS"
        | Android -> "android"
        |> Json.Optic.set Json.String_

type OpenUriTarget =
    {
        Os : OpenUriTargetOS
        Uri : Uri
    }
    static member ToJson (target:OpenUriTarget) =
        json {
            do! Json.write "os" target.Os
            do! Json.write "uri" target.Uri.AbsoluteUri
        }

type Action =
    | OpenUri of Name:string * Targets:OpenUriTarget list
    static member ToJson (action:Action) =
        json {
            match action with
            | OpenUri (name, targets) ->
                do! Json.write "@type" "OpenUri"
                do! Json.write "name" name
                do! Json.write "targets" targets
        }

type MessageCard =
    {
        ThemeColor : string
        Summary : string
        Sections : Section list
        PotentialActions : Action List
    }
    static member ToJson (messageCard:MessageCard) =
        json {
            do! Json.write "@type" "MessageCard"
            do! Json.write "@context" "http://schema.org/extensions"
            do! Json.write "themeColor" messageCard.ThemeColor
            do! Json.write "summary" messageCard.Summary
            do! Json.write "sections" messageCard.Sections
            do! Json.writeUnlessDefault "potentialAction" [] messageCard.PotentialActions
        }

type TeamsWebHookConfig =
    {
        Client : HttpClient
        WebHookUri : System.Uri
        ErrorCallback : System.Net.HttpStatusCode * string -> unit
    }

module MessageCard =
    let format (messageCard:MessageCard) =
        messageCard |> Json.serialize |> Json.formatWith JsonFormattingOptions.Pretty
    
    let private toTitleCase = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase
    
    let dataFromJson (json:Chiron.Json) =
        match json with
        | Chiron.Json.String s -> s
        | Chiron.Json.Number n -> string n
        | Chiron.Json.Bool b -> string b
        | Chiron.Json.Null _ -> String.Empty
        | json -> Json.format json
    
    let severity (severity:int) =
        if severity = 0 then "Critical"
        elif severity = 1 then "Error"
        elif severity = 2 then "Warning"
        else "Information"

    let ofAzureAlert (alert:AzureAlert.LogAlert) : MessageCard seq option =
        let alertAction = Action.OpenUri (Name="View Logs", Targets=[{Os=OpenUriTargetOS.Default; Uri=alert.Data.LinkToSearchResults |> Uri}])
        let activityTitle = alert.Data.Severity |> severity
        let activitySubtitle = String.Format ("{0} to {1}",
                                              alert.Data.SearchIntervalStartTime,
                                              alert.Data.SearchIntervalEndTime)
        alert.Data.SearchResult.Tables |> List.tryHead |> Option.map
            (
            fun table ->
                seq {
                    if table.Rows.Length > 0 then
                        for row in table.Rows do
                            let item = Seq.zip table.Columns row
                            let facts =
                                item
                                |> Seq.filter (fun (column, _) -> column.Name <> "message")
                                |> Seq.filter (fun (column, row) -> match row with | Json.Null _ -> false | _ -> true )
                                |> Seq.map (fun (column, row) -> { Name = column.Name.Replace("customDimensions_", ""); Value = dataFromJson row })
                            let message = item |> Seq.tryFind (fun (column, _) -> column.Name = "message") |> Option.map (fun (_, row) -> dataFromJson row)
                            yield
                                {
                                    ThemeColor = "0076D7"
                                    Summary = alert.Data.AlertRuleName
                                    PotentialActions = [ alertAction ]
                                    Sections =
                                        [
                                            yield
                                                {
                                                    Title = None
                                                    StartGroup = None
                                                    ActivityTitle = Some activityTitle
                                                    ActivitySubtitle = Some activitySubtitle
                                                    ActivityImage = None
                                                    ActivityText = alert.Data.Description |> Option.ofObj
                                                    Facts = Some (facts |> List.ofSeq)
                                                    Text = None
                                                }
                                            if message.IsSome then
                                                yield
                                                    {
                                                        Title = None
                                                        StartGroup = None
                                                        ActivityTitle = None
                                                        ActivitySubtitle = None
                                                        ActivityImage = None
                                                        ActivityText = None
                                                        Facts = None
                                                        Text = Some (String.Format("""<pre>{0}</pre>""", (System.Net.WebUtility.HtmlEncode(message.Value))))
                                                    }
                                        ]
                                }
                    elif not (String.IsNullOrWhiteSpace alert.Data.Description) then
                        yield
                            {
                                ThemeColor = "0076D7"
                                Summary = alert.Data.AlertRuleName
                                PotentialActions = [ alertAction ]
                                Sections =
                                    [
                                        {
                                            Title = None
                                            StartGroup = None
                                            ActivityTitle = Some activityTitle
                                            ActivitySubtitle = Some activitySubtitle
                                            ActivityImage = None
                                            ActivityText = alert.Data.Description |> Option.ofObj
                                            Facts = None
                                            Text = None
                                        }
                                    ]
                            }
                }
            )
    
    let sendToTeams (config:TeamsWebHookConfig) (messageCards:MessageCard seq) =
        async {
            for messageCard in messageCards do
                use content = new StringContent (messageCard |> format, System.Text.Encoding.UTF8, ContentTypes.TeamsCardO365Connector)
                use request = new HttpRequestMessage (HttpMethod.Post, config.WebHookUri, Content=content)
                use! response = request |> config.Client.SendAsync |> Async.AwaitTask
                if not response.IsSuccessStatusCode then
                    let! responseBody = response.Content.ReadAsStringAsync () |> Async.AwaitTask
                    config.ErrorCallback (response.StatusCode, responseBody)
        } |> Some
