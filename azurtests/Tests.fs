module Tests

open System
open Xunit
open azurts.AzureAlert
open azurts.SlackWebHook

open azurts

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
                    Section.Text (Markdown ":fire: There were some errors going on")
                    Section.Fields
                        [
                            Markdown ("*Tenant*\n50")
                            LabeledText ("Site", "SiteA")
                            LabeledText ("Resource Name", "Resource 1")
                        ]
                    Section.Text (Markdown "```Some serious stuff went down.```")
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
