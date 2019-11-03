module Tests

open System
open Xunit
open azurts.AzureAlert
open azurts.Hook
open azurts.SlackWebHook

[<Fact>]
let ``Parse Alert`` () =
    let json = System.IO.File.ReadAllText "azuresample.json"
    let (alert:LogAlert) = json |> LogAlert.parse
    ()

[<Fact>]
let ``Format Webhook Payload`` () =
    let payload =
        {
            Channel = "some_channel"
            Blocks =
                [
                    Section (Text (Markdown ":fire: There were some errors going on"))
                    Section (Fields
                        [
                            Markdown ("*Tenant*\n50")
                            LabeledText ("Site", "SiteA")
                            LabeledText ("Resource Name", "Resource 1")
                        ]
                    )
                    Section (Text (Markdown "```Some serious stuff went down.```"))
                ]
        }
    let json = payload |> Payload.format
    System.IO.File.WriteAllText ("webhookpayload.json", json)
    ()

[<Fact>]
let ``Alert to WebHook`` () =
    let json = System.IO.File.ReadAllText "azuresample.json"
    let (alert:LogAlert) = json |> LogAlert.parse
    let payloads = Payload.ofAzureAlert "alertChannel" alert
    payloads.Value |> Seq.iteri
        (fun idx payload ->
            let json = payload |> Payload.format
            System.IO.File.WriteAllText (String.Format("webhookpayload{0}.json", idx), json)
        )
    ()

[<Fact>]
let ``Compose Handlers`` () =
    /// Parses incoming Azure Alert payload
    let incomingAzAlert =
        fun json ->
            let (alert:LogAlert) = json |> LogAlert.parse
            Some alert
    /// Filters out items we don't want.
    let filteringHook =
        fun (alert:LogAlert) ->
            // Just process the first table
            alert.Data.SearchResult.Tables |> Seq.tryHead |> Option.map
                (fun table ->
                    let importantAlerts = 
                        match table.Columns |> List.tryFindIndex (fun col -> col.Name.Contains("severityLevel", StringComparison.InvariantCultureIgnoreCase)) with
                        | Some severityIndex ->
                            // Severity > 2 = error.
                            table.Rows |> Seq.filter (fun row -> (row |> Seq.item severityIndex) = Chiron.Json.Number 2M)
                        | None -> Seq.empty
                    let filteredTable = { table with Rows = importantAlerts |> List.ofSeq }
                    {
                        alert with Data = {
                                alert.Data with SearchResult = {
                                            alert.Data.SearchResult with
                                                Tables = [ filteredTable ]
                                        }
                            }
                    }
                )
                    
    // Builds a slack webhook payload
    let slackHook =
        fun (alert:LogAlert) ->
            Payload.ofAzureAlert "alertChannel" alert
    // Since it's just a test, write to files.
    let writeToFiles =
        fun (payloads:Payload seq) ->
            payloads |> Seq.iteri
                (fun idx payload ->
                    let json = payload |> Payload.format
                    System.IO.File.WriteAllText (String.Format("webhookpayloadcomposed{0}.json", idx), json)
                )
            |> Some
    let composed = incomingAzAlert >=> filteringHook >=> slackHook >=> writeToFiles
    let json = System.IO.File.ReadAllText "azuresample.json"
    match json |> composed with
    | Some () -> printfn "handled alerts"
    | None -> failwith "didn't handle alerts"
