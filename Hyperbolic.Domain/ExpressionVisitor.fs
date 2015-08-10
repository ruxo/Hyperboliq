﻿namespace Hyperboliq.Domain

module ExpressionVisitor =
    open Hyperboliq
    open Stream
    open System.Linq
    open System.Linq.Expressions

    type ExpressionVisitorConfig = {
        IsUpdate : bool
    }

    let ToBinaryOperation et =
        match et with
        | ExpressionType.Equal -> Equal
        | ExpressionType.NotEqual -> NotEqual
        | ExpressionType.GreaterThan -> GreaterThan
        | ExpressionType.GreaterThanOrEqual -> GreaterThanOrEqual
        | ExpressionType.LessThan -> LessThan
        | ExpressionType.LessThanOrEqual -> LessThanOrEqual
        | ExpressionType.AndAlso -> BinaryOperation.And
        | ExpressionType.OrElse -> BinaryOperation.Or
        | ExpressionType.Add -> Add
        | ExpressionType.Subtract -> Subtract
        | ExpressionType.Multiply -> Multiply
        | ExpressionType.Divide -> Divide
        | ExpressionType.Modulo -> Modulo
        | ExpressionType.Coalesce -> Coalesce
        | _ -> failwith "Not implemented"

    type EvaluationBinding = string * ITableReference
    type EvaluationContext = EvaluationBinding list

    let ParamName (binding : EvaluationBinding) = fst binding
    let TableRef (binding : EvaluationBinding) = snd binding

    let FindBinding ctx paramName =
        Seq.find (fun b -> (ParamName b) = paramName) ctx

    let (|SqlParameterExpression|_|) (m : MemberExpression) =
        match m with
        | x when typeof<ExpressionParameter>.IsAssignableFrom x.Type ->
            match x.Expression with
            | :? ConstantExpression as ce -> Some (m.Member.Name, ce)
            | _ -> None
        | _ -> None

    let (|ParameterExpression|_|) (m : MemberExpression) = 
        match m.Expression with
        | :? ParameterExpression as pexp -> Some (m.Member.Name, m.Type, pexp)
        | _ -> None

    let (|StringConcatenation|_|) (op : BinaryOperation, lhs : ValueNode, rhs : ValueNode) =
        match op with
        | Add -> 
            match lhs, rhs with 
            | ValueNode.Column(_, t1, _), ValueNode.Column(_, t2, _) when t1 = typeof<System.String> && t2 = typeof<System.String> ->
                Some([ lhs; rhs ])
            | ValueNode.FunctionCall(FunctionType.Concat, args), ValueNode.Column(_, t, _) when t = typeof<System.String> ->
                Some(args @ [ rhs ])
            | ValueNode.FunctionCall(FunctionType.Concat, args1), ValueNode.FunctionCall(FunctionType.Concat, args2) ->
                Some(args1 @ args2)
            | ValueNode.Column(_, t, _), ValueNode.FunctionCall(FunctionType.Concat, args) when t = typeof<System.String> ->
                Some(lhs :: args)
            | _ -> None
        | _ -> None

    let VisitSqlParameter mbrName (exp : ConstantExpression) : ValueNode =
        let flags = System.Reflection.BindingFlags.Instance ||| System.Reflection.BindingFlags.Public ||| System.Reflection.BindingFlags.NonPublic
        let fieldInfo = exp.Value.GetType().GetField(mbrName, flags)
        match fieldInfo.GetValue(exp.Value) with
        | :? ExpressionParameter as p -> ValueNode.Parameter(ParameterToken(p.Name))
        | _ -> failwith "Not implemented"

    let VisitMemberAccess (cfg : ExpressionVisitorConfig) (exp : MemberExpression) (context : EvaluationContext) : ValueNode =
        match exp with
        | SqlParameterExpression (mbrName, expr) -> VisitSqlParameter mbrName expr 
        | ParameterExpression (mbrName, mbrType, expr) -> 
            let binding = FindBinding context expr.Name
            ValueNode.Column(mbrName, mbrType, TableRef binding)
        | _ -> failwith "Not implemented"

    let VisitConstant (exp : ConstantExpression) : ValueNode =
        match exp.Value with
        | null -> ValueNode.NullValue
        |  :? SelectExpression as se -> 
            match se with 
            | Plain(q) -> ValueNode.SubExpression(q)
            | _ -> failwith "Not implemented"
        | :? string as s-> ValueNode.Constant(ConstantNode(sprintf "'%s'" s))
        | x ->  ValueNode.Constant(ConstantNode(x.ToString()))

    let (|CompiledNullLambda|_|) (e : Expression) : ValueNode option =
        try
            let result = Expression.Lambda(e).Compile().DynamicInvoke()
            match result with
            | null -> 
                Some ValueNode.NullValue
            | :? ValueNode as vn -> 
                Some vn
            | :? ISelectExpressionTransformable as ss -> 
                match ss.ToSelectExpression() with
                | Plain(q) -> Some(ValueNode.SubExpression(q))
                | _ -> None
            | :? SelectExpression as ss ->
                match ss with
                | Plain(q) -> Some (ValueNode.SubExpression(q))
                | _ -> None
            | _ -> 
                Some (ValueNode.Constant(ConstantNode(result.ToString())))
        with
            | _ -> None

    let rec VisitSqlMethodCall (cfg : ExpressionVisitorConfig) (exp : MethodCallExpression) (context : EvaluationContext) : ValueNode = 
        let args = VisitExpressionList cfg <| List.ofArray (exp.Arguments.ToArray()) <| context
        match exp.Method.Name, args with
        | "In", ValueList([ lhs; rhs ]) -> ValueNode.BinaryExpression({ Lhs = lhs; Rhs = rhs; Operation = BinaryOperation.In })
        | "SubExpr", ValueList([ expr ]) -> expr
        | "Max", ValueList([ expr ]) -> ValueNode.Aggregate(Max, expr)
        | "Min", ValueList([ expr ]) -> ValueNode.Aggregate(Min, expr)
        | "Avg", ValueList([ expr ]) -> ValueNode.Aggregate(Avg, expr)
        | "Sum", ValueList([ expr ]) -> ValueNode.Aggregate(Sum, expr)
        | "Count", ValueList(_) -> ValueNode.Aggregate(Count, ValueNode.NullValue)
        | "RowNumber", ValueList(_) -> ValueNode.Aggregate(RowNumber, ValueNode.NullValue)
        | _ -> failwith "Not implemented"

    and VisitMethodCall (cfg : ExpressionVisitorConfig) (exp : MethodCallExpression) context : ValueNode =
        let VisitStringMethodCall (smc : MethodCallExpression) context =
            match smc.Method.Name with
            | "ToUpper" | "ToUpperInvariant" -> 
                ValueNode.FunctionCall(FunctionType.Upper, [ VisitMemberAccess cfg (smc.Object :?> MemberExpression) context ])
            | "ToLower" | "ToLowerInvariant" ->
                ValueNode.FunctionCall(FunctionType.Lower, [ VisitMemberAccess cfg (smc.Object :?> MemberExpression) context ])
            | _ -> failwith "Not implemented"

        match exp with
        | x when x.Method.DeclaringType = typeof<Sql> -> VisitSqlMethodCall cfg exp context
        | x when x.Method.DeclaringType = typeof<System.String> -> VisitStringMethodCall x context
        | CompiledNullLambda stream -> stream
        | _ -> failwith "Not implemented"

        
    and VisitUnary (cfg : ExpressionVisitorConfig) (exp : UnaryExpression) context =
        match exp with
        | x when x.NodeType = ExpressionType.Convert && typeof<ExpressionParameter>.IsAssignableFrom(x.Operand.Type) -> 
            InternalVisit cfg exp.Operand context
        | x when x.NodeType = ExpressionType.Convert && x.Method <> null ->
            match Expression.Lambda(x).Compile().DynamicInvoke() with
            | null -> ValueNode.NullValue
            | x -> ValueNode.Constant(ConstantNode(x.ToString()))
        | _ -> InternalVisit cfg exp.Operand context

    and VisitBinary cfg (exp : BinaryExpression) (context : EvaluationContext) : ValueNode = 
        let lhs = InternalVisit cfg exp.Left context 
        let rhs = InternalVisit cfg exp.Right context 
        let op = ToBinaryOperation exp.NodeType
        match op, lhs, rhs with
        | StringConcatenation args -> ValueNode.FunctionCall(FunctionType.Concat, args)
        | _ -> ValueNode.BinaryExpression({ Lhs = lhs; Operation = op; Rhs = rhs })

    and VisitNew cfg (exp : NewExpression) (context : EvaluationContext) : ValueNode =
        let VisitColumnReference (mbr : System.Reflection.MemberInfo) (arg : Expression) (context : EvaluationContext) : ValueNode =
            let value = InternalVisit cfg arg context
            match value, cfg.IsUpdate with
            | ValueNode.Column(c, _, _), _ when c <> mbr.Name ->
                ValueNode.NamedColumn({ Alias = mbr.Name; Column = value })
            | ValueNode.Aggregate(_), _
            | ValueNode.WindowedColumn(_), _
            | ValueNode.Constant(_), _
            | ValueNode.BinaryExpression(_), false ->
                ValueNode.NamedColumn({ Alias = mbr.Name; Column = value })
            | _ -> value

        let mbr = Expression.Convert(exp, typeof<System.Object>) 
        let lmb = Expression.Lambda<System.Func<System.Object>>(mbr)
        try 
            let getter = lmb.Compile()
            let result = getter.DynamicInvoke()
            match result with
            | :? ExpressionParameter as param -> ValueNode.Parameter(ParameterToken(param.Name))
            | _ -> 
                let binding = FindBinding context (getter.ToString())
                ValueNode.Column(ParamName binding, result.GetType(), TableRef binding)
        with
            | :? System.Collections.Generic.KeyNotFoundException 
            | :? System.InvalidOperationException -> 
                let members = List.ofSeq exp.Members
                let arguments = List.ofSeq exp.Arguments
                List.zip members arguments 
                |> List.map (fun (mbr, arg) -> VisitColumnReference mbr arg context)
                |> ValueNode.ValueList

    and VisitExpressionList (cfg : ExpressionVisitorConfig) (expList : Expression list) (context : EvaluationContext) : ValueNode =
        let HandleSingleExpression (context : EvaluationContext) (exp : Expression) : ValueNode =
            match exp with
            | :? NewArrayExpression as arrayExp -> 
                VisitExpressionList cfg <| List.ofArray (arrayExp.Expressions.ToArray()) <| context 
            | _ -> InternalVisit cfg exp context

        ValueNode.ValueList(List.map (HandleSingleExpression context) expList)
        

    and InternalVisit (cfg : ExpressionVisitorConfig) (exp : Expression) context : ValueNode =
        match exp with
        | null -> ValueNode.NullValue
        | :? LambdaExpression as x -> InternalVisit cfg x.Body context
        | :? NewExpression as x -> VisitNew cfg x context
        | :? MemberExpression as x -> VisitMemberAccess cfg x context
        | :? BinaryExpression as x -> VisitBinary cfg x context
        | :? ConstantExpression as x -> VisitConstant x
        | :? UnaryExpression as x -> VisitUnary cfg x context
        | :? MethodCallExpression as x -> VisitMethodCall cfg x context
        | _ -> failwith "Not implemented"

    let BindEvaluationContext (exp : LambdaExpression) (context : ITableReference seq) : EvaluationContext =
        exp.Parameters
        |> Seq.map (fun p -> p.Name)
        |> Seq.zip <| context
        |> List.ofSeq


    let VisitWithCustomConfig (cfg : ExpressionVisitorConfig) (exp : Expression) (context : ITableReference seq) : ValueNode option =
        match exp with
        | null -> None
        | :? LambdaExpression as x -> BindEvaluationContext x context |> InternalVisit cfg exp |> fun r -> Some(r)
        | _ -> failwith "Not implemented"

    let Visit exp ctx = VisitWithCustomConfig { IsUpdate = false } exp ctx
