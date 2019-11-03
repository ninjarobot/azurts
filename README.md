# azurts
Extending Azure alerts with webhook infrastructure

[![Azurts on Nuget](https://buildstats.info/nuget/azurts)](https://www.nuget.org/packages/azurts/)

Azure Monitor includes alerts that can send the alert data to a webhook, and those often need some adjustment to send a meaningful message to other systems, such as Slack. This provides a library and usage example for accepting a custom log alert from Azure as a webhook, optionally performing some intermediate processing and then reformatting the data to send to a webhook for Slack.

The processing pipeline is made of composable parts so that different customizations can be included when handling a webhook. It uses a ["railway"](https://fsharpforfunandprofit.com/posts/recipe-part2/) style of composition that makes it relatively simple to chain together a series of parts.

```fsharp
type Hook = a' -> b' option

// composing multiple Hook functions with a railroad operator
let webhookPipeline = incomingAzAlert >=> filteringHook >=> slackHook
```

The `incomingAzAlert` accepts the Azure alert JSON body and parses it into a AzureAlert type. This is passed to `filteringHook` which determines if the alert is one that needs to be published. If so, it is passed to `slackHook` which creates the JSON necessary for a Slack webhook and then posts that to the URL of that Slack webhook.
