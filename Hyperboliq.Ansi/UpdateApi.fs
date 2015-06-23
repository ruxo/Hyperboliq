﻿namespace Hyperboliq

open System
open System.Linq.Expressions
open Hyperboliq
open Hyperboliq.Types
open Hyperboliq.Domain.Stream
open Hyperboliq.Domain.ExpressionParts
open Hyperboliq.Domain.UpdateExpressionPart
open Hyperboliq.Domain.SqlGen

type UpdateWhere<'a> internal (expr : UpdateExpression) =
    static let New expr = UpdateWhere<'a>(expr)

    member x.And(predicate : Expression<Func<'a, bool>>) =
        { expr with Where = Some(AddOrCreateWhereAndClause expr.Where predicate [| TableReferenceFromType<'a> |]) }
        |> New
    member x.Or(predicate : Expression<Func<'a, bool>>) =
        { expr with Where = Some(AddOrCreateWhereOrClause expr.Where predicate [| TableReferenceFromType<'a> |]) }
        |> New

    member x.ToSqlExpression () = (x :> ISqlExpressionTransformable).ToSqlExpression ()
    interface ISqlExpressionTransformable with
        member x.ToSqlExpression () = SqlExpression.Update(expr)

    member x.ToSql (dialect : ISqlDialect) = (x :> ISqlStatement).ToSql(dialect)
    interface ISqlStatement with
        member x.ToSql(dialect : ISqlDialect) = x.ToSqlExpression() |> SqlifyExpression dialect

type UpdateSet<'a> internal (expr : UpdateExpression) =
    static let New expr = UpdateSet<'a>(expr)

    new() = UpdateSet<'a>({ UpdateSet = { Table = TableReferenceFromType<'a>; SetExpressions = [] }; Where = None })

    member x.Set<'b>(selector : Expression<Func<'a, 'b>>, value : 'b) =
        { expr with UpdateSet = (AddObjectSetExpression expr.UpdateSet selector value) }
        |> New

    member x.Set<'b>(selector : Expression<Func<'a, 'b>>, valueUpdate : Expression<Func<'a, 'b>>) =
        { expr with UpdateSet = (AddValueExpression expr.UpdateSet selector valueUpdate)}
        |> New

    member x.Set<'b>(selector : Expression<Func<'a, 'b>>, selectExpr : SelectExpression) = 
        { expr with UpdateSet = (AddSingleValueSetExpression expr.UpdateSet selector selectExpr) }
        |> New

    member x.Set<'b>(selector : Expression<Func<'a, 'b>>, selectExpr : ISelectExpressionTransformable) =
        x.Set(selector, selectExpr.ToSelectExpression())

    member x.Where(predicate : Expression<Func<'a, bool>>) = UpdateWhere(expr).And(predicate)

    member x.ToSqlExpression () = (x :> ISqlExpressionTransformable).ToSqlExpression ()
    interface ISqlExpressionTransformable with
        member x.ToSqlExpression () = SqlExpression.Update(expr)

    member x.ToSql (dialect : ISqlDialect) = (x :> ISqlStatement).ToSql(dialect)
    interface ISqlStatement with
        member x.ToSql(dialect : ISqlDialect) = x.ToSqlExpression() |> SqlifyExpression dialect

type Update<'a> private () =
    static member Set<'b>(selector : Expression<Func<'a, 'b>>, value : 'b) = UpdateSet<'a>().Set(selector, value)
    static member Set<'b>(selector : Expression<Func<'a, 'b>>, valueUpdate : Expression<Func<'a, 'b>>) = UpdateSet<'a>().Set(selector, valueUpdate)
    static member Set<'b>(selector : Expression<Func<'a, 'b>>, selectExpr : SelectExpression) = UpdateSet<'a>().Set(selector, selectExpr)
    static member Set<'b>(selector : Expression<Func<'a, 'b>>, selectExpr : ISelectExpressionTransformable) = UpdateSet<'a>().Set(selector, selectExpr)