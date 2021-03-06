﻿namespace Hyperboliq.Domain

open Hyperboliq

type Sql private () =
    static member SubExpr<'a> (e : ISqlQuery) = Unchecked.defaultof<'a>
    static member In<'a> (value : 'a) (e : ISqlQuery) = false
    static member Max<'a> (value : 'a) = Unchecked.defaultof<'a>
    static member Min<'a> (value : 'a) = Unchecked.defaultof<'a>
    static member Avg<'a> (value : 'a) = Unchecked.defaultof<'a>
    static member Count () = Unchecked.defaultof<int>
    static member Sum<'a> (value : 'a) = Unchecked.defaultof<'a>
    static member RowNumber () = Unchecked.defaultof<int>
    static member Parameter (param : ExpressionParameter<'a>) = Unchecked.defaultof<'a>