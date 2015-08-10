﻿using Hyperboliq.Tests.Model;
using Hyperboliq.Domain;
using Hyperboliq.Dialects;
using Xunit;
using S = Hyperboliq.Tests.SqlStreamExtensions;
using BinaryOperation = Hyperboliq.Domain.AST.BinaryOperation;

namespace Hyperboliq.Tests.SqlGeneration
{
    [Trait("SqlGeneration", "StandardFunctions")]
    public class SqlGeneration_SqlFunctionsTests
    {

        [Fact]
        public void ItCanGenerateSqlForToUpper()
        {
            var stream =
                S.SelectNode(
                    S.Select(S.Func(AST.FunctionType.Upper, new[] { S.Col<Person>("Name") })),
                    S.From<Person>(),
                    S.Where(
                        S.BinExp(
                            S.Func(AST.FunctionType.Upper, new[] { S.Col<Person>("Name") }),
                            BinaryOperation.Equal,
                            S.Const("'KALLE'"))
                    ));
            var result = SqlGen.SqlifyExpression(AnsiSql.Dialect, stream);

            string expected = "SELECT UPPER(PersonRef.Name) FROM Person PersonRef WHERE UPPER(PersonRef.Name) = 'KALLE'";
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ItCanGenerateSqlForToLower()
        {
            var stream =
                S.SelectNode(
                    S.Select(S.Func(AST.FunctionType.Lower, new[] { S.Col<Person>("Name") })),
                    S.From<Person>(),
                    S.Where(
                        S.BinExp(
                            S.Func(AST.FunctionType.Lower, new[] { S.Col<Person>("Name") }),
                            BinaryOperation.Equal,
                            S.Const("'kalle'"))
                    ));
            var result = SqlGen.SqlifyExpression(AnsiSql.Dialect, stream);

            string expected = "SELECT LOWER(PersonRef.Name) FROM Person PersonRef WHERE LOWER(PersonRef.Name) = 'kalle'";
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ItCanGenerateSqlForAConcatFunctionCall()
        {
            var stream =
                S.SelectNode(
                    S.Select(S.Func(AST.FunctionType.Concat, new[] { S.Col<Person>("Name"), S.Col<Car>("Brand"), S.Col<Person>("Name") })),
                    S.From<Person>(
                        S.Join<Person, Car>(
                            AST.JoinType.InnerJoin, 
                            S.BinExp(S.Col<Person>("Id"), BinaryOperation.Equal, S.Col<Car>("DriverId")))),
                    S.Where(
                        S.BinExp(
                            S.Func(AST.FunctionType.Concat, new[] { S.Col<Person>("Name"), S.Col<Car>("Brand"), }),
                            BinaryOperation.Equal,
                            S.Const("'kallesaab'"))));
            var result = SqlGen.SqlifyExpression(AnsiSql.Dialect, stream);

            string expected =
                "SELECT CONCAT(PersonRef.Name, CarRef.Brand, PersonRef.Name) " +
                "FROM Person PersonRef " +
                "INNER JOIN Car CarRef ON PersonRef.Id = CarRef.DriverId " +
                "WHERE CONCAT(PersonRef.Name, CarRef.Brand) = 'kallesaab'";
            Assert.Equal(expected, result);
        }
    }
}
