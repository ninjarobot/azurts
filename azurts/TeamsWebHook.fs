module azurts.TeamsWebHook

open System
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
            do! Json.write "uri" (string target.Uri)
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
