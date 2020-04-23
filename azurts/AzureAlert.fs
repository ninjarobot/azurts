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
type DataSource =
    {
        ResourceId : string
        Tables : string list
    }
    static member FromJson (_:DataSource) =
        json {
            let! resourceId = Json.read "resourceId"
            let! tables = Json.read "tables"
            return
                {
                    ResourceId = resourceId
                    Tables = tables
                }
        }
type SearchResult =
    {
        Tables : Table list
        DataSources : DataSource list
    }
    static member FromJson (_:SearchResult) =
        json {
            let! tables = Json.read "tables"
            let! dataSources = Json.read "dataSources"
            return
                {
                    Tables = tables
                    DataSources = dataSources
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
module Alert =
    let parse json =
        let (alert:LogAlert) = json |> Json.parse |> Json.deserialize
        alert
    
    /// Parses and deserializes JSON into Some LogAlert or None if there
    /// are parsing or deserialization errors.
    let tryParse json : LogAlert option =
        match Json.tryParse json with
        | Choice1Of2 parsed ->
            match Json.tryDeserialize parsed with
            | Choice1Of2 alert -> Some alert
            | Choice2Of2 _ -> None
        | Choice2Of2 _ -> None

module Filters =
    /// Includes alerts that are above an inclusive minimum alert severity
    let minimumSeverity (minimum:int) =
        fun (alert:LogAlert) ->
            if alert.Data.Severity >= minimum then Some alert
            else None
    
    /// Includes only alerts that are below an inclusive maximum alert severity
    let maximumSeverity (maximum:int) =
        fun (alert:LogAlert) ->
            if alert.Data.Severity <= maximum then Some alert
            else None
    
    /// Includes only alerts for the given subscription
    let subscriptionId (sub:string) =
        fun (alert:LogAlert) ->
            if String.IsNullOrEmpty (alert.Data.SubscriptionId) then None
            elif alert.Data.SubscriptionId.Equals (sub, StringComparison.InvariantCultureIgnoreCase) then Some alert
            else None
    
    /// Filters based on a column value in the search result data.
    let fieldValue (fieldName:string) (expectedValue:string) =
        fun (alert:LogAlert) ->
            alert.Data.SearchResult.Tables |> Seq.tryHead |> Option.map
                (fun table ->
                    let importantAlerts =
                        match table.Columns |> List.tryFindIndex (fun col -> col.Name.ToLowerInvariant().Contains(fieldName.ToLowerInvariant())) with
                        | Some rowIndex ->
                            let colNames = table.Columns |> List.map (fun c -> c.Name)
                            seq {
                                for row in table.Rows do
                                    let lookup = Seq.zip colNames row |> dict
                                    if lookup.[fieldName] = (expectedValue |> Json.serialize) then
                                        yield row
                                    //let d = row |> Seq.item rowIndex |> Json.tryDeserialize |> function Choice1Of2 s -> Some s | Choice2Of2 _ -> None
                                    //if d = Some(expectedValue) then
                                    //    yield row
                            }
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
