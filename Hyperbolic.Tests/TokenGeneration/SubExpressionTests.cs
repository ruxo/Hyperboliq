﻿using Xunit;
using Hyperboliq.Tests.Model;
using static Hyperboliq.Tests.SqlStreamExtensions;
using static Hyperboliq.Domain.Stream;
using static Hyperboliq.Domain.Types;

namespace Hyperboliq.Tests
{
    [Trait("TokenGeneration", "Subexpressions")]
    public class TokenGeneration_SubExpressionTests
    {
        [Fact]
        public void ItShouldBePossibleToCompareAgainstASubExpressionInAWhereExpression()
        {
            var expr = Select.Star<Person>()
                             .From<Person>()
                             .Where<Person>(p => p.Age > Sql.SubExpr<int>(Select.Column<Car>(c => c.Age)
                                                                                .From<Car>()
                                                                                .Where<Car>(c => c.Id == 42)));
            var result = expr.ToSqlStream();

            var expected =
                StreamFrom(
                    Select(Col<Person>("*")),
                    From<Person>(),
                    Where(
                        BinExp(
                            Col<Person>("Age"),
                            BinaryOperation.GreaterThan,
                            SubExp(
                                StreamFrom(
                                    Select(Col<Car>("Age")),
                                    From<Car>(),
                                    Where(BinExp(Col<Car>("Id"), BinaryOperation.Equal, Const(42))))))));
            result.ShouldEqual(expected);
        }

        [Fact]
        public void ItShouldBePossibleToDoAnInQueryWithASubExpression()
        {
            var expr = 
                Select.Star<Person>()
                      .From<Person>()
                      .Where<Person>(p => Sql.In(p.Id, Select.Column<Car>(c => c.DriverId).From<Car>()));
            var result = expr.ToSqlStream();

            var expected =
                StreamFrom(
                    Select(Col<Person>("*")),
                    From<Person>(),
                    Where(
                        BinExp(
                            Col<Person>("Id"),
                            BinaryOperation.In,
                            SubExp(
                                StreamFrom(
                                    Select(Col<Car>("DriverId")),
                                    From<Car>())))));
            result.ShouldEqual(expected);
        }
    }
}
