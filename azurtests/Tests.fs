module Tests

open System
open Xunit
open azurts.AzureAlert
open azurts.Hook
open azurts.SlackWebHook

[<Fact>]
let ``Parse Alert`` () =
    let json = System.IO.File.ReadAllText "azuresample.json"
    let (alert:LogAlert) = json |> Alert.parse
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
    let (alert:LogAlert) = json |> Alert.parse
    let payloads = Payload.ofAzureAlert "alertChannel" alert
    payloads.Value |> Seq.iteri
        (fun idx payload ->
            let json = payload |> Payload.format
            System.IO.File.WriteAllText (String.Format("webhookpayload{0}.json", idx), json)
        )
    ()

/// Parses incoming Azure Alert payload
let incomingAzAlert =
    fun json ->
        let (alert:LogAlert) = json |> Alert.parse
        Some alert

[<Fact>]
let ``Compose Handlers`` () =
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

[<Fact>]
let ``Minimum severity filter`` () =
    let onlySev4 = incomingAzAlert >=> Filters.minimumSeverity 4
    System.IO.File.ReadAllText "azuresample.json"
    |> onlySev4 |> Option.iter (fun _ -> failwith "Got an alert that wasn't minimum severity 4")

[<Fact>]
let ``Maximum severity filter`` () =
    let belowSev3 = incomingAzAlert >=> Filters.maximumSeverity 2
    System.IO.File.ReadAllText "azuresample.json"
    |> belowSev3 |> Option.iter (fun _ -> failwith "Got an alert that wasn't maximum severity 2")

[<Fact>]
let ``Subscription filter (exclusive)`` () =
    let randomSubscription = incomingAzAlert >=> Filters.subscriptionId (Guid.NewGuid().ToString())
    System.IO.File.ReadAllText "azuresample.json"
    |> randomSubscription |> Option.iter (fun _ -> failwith "Got an alert when subscription didn't match")

[<Fact>]
let ``Subscription filter (inclusive)`` () =
    let randomSubscription = incomingAzAlert >=> Filters.subscriptionId ("bda4d6df-29e2-48f1-acfa-7E024792d1c5")
    System.IO.File.ReadAllText "azuresample.json"
    |> randomSubscription |> function
    | Some _ -> ()
    | None -> failwith "Filtered out alert that should match subscription"

[<Fact>]
let ``Alert custom field found`` () =
    let byResourceName = incomingAzAlert >=> Filters.fieldValue "customDimensions_ResourceName" "ResourceOne"
    System.IO.File.ReadAllText "azuresample.json"
    |> byResourceName |> function
    | Some _ -> ()
    | None -> failwith "Expected to find by ResourceName"

[<Fact>]
let ``Broadcast to many`` () =
    let mutable one = false
    let mutable two = false
    
    let toMany =
        broadcast
            [
                fun (_:LogAlert) -> async { one <- true } |> Some
                fun (_:LogAlert) -> async { two <- true } |> Some
            ]
    
    let sendAlertToMany = incomingAzAlert >=> toMany
    System.IO.File.ReadAllText "azuresample.json"
    |> sendAlertToMany |> function
    | Some hook -> hook |> Async.RunSynchronously
    | None -> failwith "Expected a hook to run"
    Assert.True(one)
    Assert.True(two)

/// Make sure it returns None when there is nowhere to broadcast.
[<Fact>]
let ``Broadcast to none`` () =
    let sendAlertToNone = incomingAzAlert >=> broadcast []
    System.IO.File.ReadAllText "azuresample.json"
    |> sendAlertToNone |> function
    | Some _ -> failwith "Expected None as a hook"
    | None -> ()
