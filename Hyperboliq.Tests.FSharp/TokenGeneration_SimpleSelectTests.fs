﻿namespace Hyperboliq.Tests.TokenGeneration

module SimpleSelectTests =
    open NUnit.Framework
    open FsUnit
    open Hyperboliq
    open Hyperboliq.Domain.AST
    open Hyperboliq.Domain

    [<Test>]
    let ``It should be able to select all from a table`` () =
        let expr = Select.Star<Person>().From<Person>()
        let result = expr.ToSqlExpression()

        let tref = TableIdentifier<Person>()
        let expected =
            { TestHelpers.EmptySelect with
                Select = { IsDistinct = false; Values = [ StarColumn(tref.Reference) ] }
                From = { Tables = [ tref ]; Joins = [] } }
            |> TestHelpers.ToPlainSelect

        result |> should equal expected

    [<Test>]
    let ``It should be possible to select distinct from a table`` () =
        let expr = Select.Distinct.Star<Person>().From<Person>()
        let result = expr.ToSqlExpression()

        let tref = TableIdentifier<Person>()
        let expected =
            { TestHelpers.EmptySelect with
                Select = { IsDistinct = true; Values = [ StarColumn(tref.Reference) ] }
                From = { Tables = [ tref ]; Joins = [] } }
            |> TestHelpers.ToPlainSelect

        result |> should equal expected

    [<Test>]
    let ``It should be possible to select a constant`` () =
        let expr = Select.Column(fun (p : Person) -> let favoriteNumber = 42 in (favoriteNumber, p.Name))
                         .From<Person>()
        let result = expr.ToSqlExpression()

        let tref = TableIdentifier<Person>()
        let expected =
            { TestHelpers.EmptySelect with
                Select = { IsDistinct = false
                           Values = [ ValueNode.NamedColumn({ Alias = "favoriteNumber"; Column = ValueNode.Constant("42") })
                                      ValueNode.Column("Name", typeof<string>, tref.Reference :> ITableReference) ] } 
                From = { Tables = [ tref ]; Joins = [] } }
            |> TestHelpers.ToPlainSelect
        result |> should equal expected

    [<Test>]
    let ``It should be able to select columns`` () =
        let expr = Select.Column(<@ fun (p : Person) -> (p.Name, p.Age) @>)
                         .From<Person>()
        let result = expr.ToSqlExpression()

        let tref = TableIdentifier<Person>()
        let expected =
            { TestHelpers.EmptySelect with
                Select = { IsDistinct = false
                           Values = [ ValueNode.Column("Name", typeof<string>, tref.Reference :> ITableReference)
                                      ValueNode.Column("Age", typeof<int>, tref.Reference :> ITableReference) ] }
                From = { Tables = [ tref ]; Joins = [] } }
            |> TestHelpers.ToPlainSelect
        result |> should equal expected

    [<Test>]
    let ``It should order the columns in the expected order when calling column several times`` () =
        let expr = Select.Column(<@ fun (p : Person) -> p.Name @>).Column(<@ fun (p : Person) -> p.Age @>)
                         .From<Person>()
        let result = expr.ToSqlExpression()
        
        let tref = TableIdentifier<Person>()
        let expected =
            { TestHelpers.EmptySelect with
                Select = { IsDistinct = false
                           Values = [ ValueNode.Column("Name", typeof<string>, tref.Reference :> ITableReference)
                                      ValueNode.Column("Age", typeof<int>, tref.Reference :> ITableReference) ] }
                From = { Tables = [ tref ]; Joins = [] } }
            |> TestHelpers.ToPlainSelect
        result |> should equal expected

    [<Test>]
    let ``It should be possible to select distinct single columns from a table`` () =
        let expr = Select.Distinct.Column(<@ fun (p : Person) -> p.Age @>)
                         .From<Person>()
        let result = expr.ToSqlExpression()

        let tref = TableIdentifier<Person>()
        let expected =
            { TestHelpers.EmptySelect with
                Select = { IsDistinct = true
                           Values = [ ValueNode.Column("Age", typeof<int>, tref.Reference :> ITableReference) ] }
                From = { Tables = [ tref ]; Joins = [] } }
            |> TestHelpers.ToPlainSelect
        result |> should equal expected

    [<Test>]
    let ``It should be possible to select the number of rows from a table`` () = 
        let expr = Select.Column(<@ fun (p : Person) -> Sql.Count() @>).From<Person>()
        let result = expr.ToSqlExpression()

        let tref = TableIdentifier<Person>()
        let expected = 
            { TestHelpers.EmptySelect with
                Select = { IsDistinct = false
                           Values = [ ValueNode.Aggregate(AggregateType.Count, ValueNode.NullValue) ] }
                From = { Tables = [ tref ]; Joins = [] } }
            |> TestHelpers.ToPlainSelect
        result |> should equal expected

    [<Test>]
    let ``It should be possible to select the number of rows from a table and name the column`` () =
        let expr = Select.Column(<@ fun (p : Person) -> let numberOfPersons = Sql.RowNumber() in numberOfPersons @>)
                         .From<Person>()
        let result = expr.ToSqlExpression()

        let tref = TableIdentifier<Person>()
        let expected = 
            { TestHelpers.EmptySelect with
                Select = { IsDistinct = false
                           Values = [ ValueNode.NamedColumn({ Alias = "numberOfPersons"; Column = ValueNode.Aggregate(AggregateType.RowNumber, ValueNode.NullValue) })] }
                From = { Tables = [ tref ]; Joins = [] } }
            |> TestHelpers.ToPlainSelect
        result |> should equal expected