﻿using NUnit.Framework;
using Hyperboliq.Dialects;
using Hyperboliq.Domain;
using Hyperboliq.Tests.TokenGeneration;
using S = Hyperboliq.Tests.SqlStreamExtensions;
using BinaryOperation = Hyperboliq.Domain.AST.BinaryOperation;

namespace Hyperboliq.Tests.SqlGeneration
{
    
    [TestFixture]
    public class SqlGeneration_SimpleWhereTests
    {
        [Test]
        public void ItShouldBePossibleToSqlifyAWhere()
        {
            var stream =
                S.SelectNode(
                     S.Select(S.Star<Person>()),
                    S.From<Person>(),
                    S.Where(S.BinExp(S.Col<Person>("Age"), BinaryOperation.GreaterThan, S.Const(42))));

            var result = SqlGen.SqlifyExpression(AnsiSql.Dialect, stream);
            Assert.That(result, Is.EqualTo(@"SELECT PersonRef.* FROM Person PersonRef WHERE PersonRef.Age > 42"));
        }

        [Test]
        public void ItShouldBePossibleToSqlifyAWhereWithAndAndOr()
        {
            var stream = 
                S.SelectNode(
                    S.Select(S.Star<Person>()),
                    S.From<Person>(),
                    S.Where(
                       S.BinExp(
                           S.BinExp(S.Col<Person>("Age"), BinaryOperation.GreaterThan, S.Const(42)),
                           BinaryOperation.Or,
                           S.BinExp(
                               S.BinExp(S.Col<Person>("Age"), BinaryOperation.LessThan, S.Const(10)),
                               BinaryOperation.And,
                               S.BinExp(S.Col<Person>("Name"), BinaryOperation.Equal, S.Const("'Karl'"))
                           )
                    )));

            var result = SqlGen.SqlifyExpression(AnsiSql.Dialect, stream);
            Assert.That(result, Is.EqualTo(@"SELECT PersonRef.* FROM Person PersonRef WHERE PersonRef.Age > 42 OR PersonRef.Age < 10 AND PersonRef.Name = 'Karl'"));
        }

        [Test]
        public void ItShouldBePossibleToSqlifyAWhereWithAndOrsThatIsNotInBinaryExpressions()
        {
            var stream =
                S.SelectNode(
                    S.Select(S.Star<Person>()),
                    S.From<Person>(),
                    S.Where(
                        S.BinExp(S.Col<Person>("Age"), BinaryOperation.LessThan, S.Const(42)),
                        S.And(S.BinExp(S.Col<Person>("Age"), BinaryOperation.GreaterThan, S.Const(12))),
                        S.Or(S.BinExp(S.Col<Person>("Name"), BinaryOperation.Equal, S.Const("'Karl'")))));
            var result = SqlGen.SqlifyExpression(AnsiSql.Dialect, stream);
            Assert.That(result, Is.EqualTo(@"SELECT PersonRef.* FROM Person PersonRef WHERE PersonRef.Age < 42 AND PersonRef.Age > 12 OR PersonRef.Name = 'Karl'"));
        }
    }
}
