module azurts.AzureAlert

open System
open Chiron


type Column =
    {
        Name : string
        Type : string
    }
    static member FromJson(_:Column) =
        json {
            let! name = Json.read "name"
            let! typ = Json.read "type"
            return
                {
                    Name = name
                    Type = typ
                }
        }
type Row = Json list
type Table =
    {
        Name : string
        Columns : Column list
        Rows : Row list
    }
    static member FromJson (_:Table) =
        json {
            let! name = Json.read "name"
            let! columns = Json.read "columns"
            let! rows = Json.read "rows"
            return
                {
                    Name = name
                    Columns = columns
                    Rows = rows
                }
        }
type SearchResult =
    {
        Tables : Table list
    }
    static member FromJson (_:SearchResult) =
        json {
            let! tables = Json.read "tables"
            return
                {
                    Tables = tables
                }
        }
type LogAlertData =
    {
        SubscriptionId : string
        AlertRuleName : string
        Description : string
        SearchIntervalStartTime : DateTimeOffset
        SearchIntervalEndTime : DateTimeOffset
        Severity : int
        LinkToSearchResults : string
        SearchResult : SearchResult
    }
    static member FromJson (_:LogAlertData) =
        json {
            let! sub = Json.read "SubscriptionId"
            let! alertRuleName = Json.read "AlertRuleName"
            let! description = Json.read "Description"
            let! searchIntStart = Json.read "SearchIntervalStartTimeUtc"
            let! searchIntEnd = Json.read "SearchIntervalEndtimeUtc"
            let! severity = Json.read "Severity"
            let! link = Json.read "LinkToSearchResults"
            let! result = Json.read "SearchResult"    
            return
                {
                    SubscriptionId = sub
                    AlertRuleName = alertRuleName
                    Description = description
                    SearchIntervalStartTime = DateTimeOffset.Parse searchIntStart
                    SearchIntervalEndTime = DateTimeOffset.Parse searchIntEnd
                    Severity = Int32.Parse severity
                    LinkToSearchResults = link
                    SearchResult = result
                }
        }
type LogAlert =
    {
        SchemaId : string
        Data : LogAlertData
    }
    static member FromJson (_:LogAlert) =
        json {
            let! schemaId = Json.read "schemaId"
            let! data = Json.read "data"
            return
                {
                    SchemaId = schemaId
                    Data = data
                }
        }
module LogAlert =
    let parse json =
        let (alert:LogAlert) = json |> Json.parse |> Json.deserialize
        alert
