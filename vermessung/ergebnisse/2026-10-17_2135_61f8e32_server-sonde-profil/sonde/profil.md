# CPU-Profil des Hosts

Stichproben 56372 (≈ 10 ms je Thread), davon arbeitend (innerster Rahmen keine Warte-Methode) 45284 ≈ 452.8 Thread-Sekunden; Markierung: UNMANAGED_CODE_TIME 56345, CPU_TIME 27

Die Anteile beziehen sich auf die arbeitenden Stichproben.

## Arbeit nach Bibliothek (innerster Rahmen außerhalb der Laufzeit)

| Bibliothek | Anteil |
|---|---:|
| Marten | 66.0 % |
| FastExpressionCompiler | 9.7 % |
| Npgsql | 7.3 % |
| Infrastructure | 7.2 % |
| Laufzeit allein | 2.1 % |
| Abstractions | 2.1 % |
| Polly.Core | 1.8 % |
| Proto.Actor | 1.3 % |
| StackExchange.Redis | 0.8 % |
| Proto.Cluster | 0.6 % |
| Pipelines.Sockets.Unofficial | 0.5 % |
| Weasel.Postgresql | 0.5 % |
| JasperFx.Events | 0.0 % |
| Anonymously Hosted DynamicMethods Assembly | 0.0 % |
| bq3fyjzg.le0 | 0.0 % |
| Proto.Remote | 0.0 % |
| Consul | 0.0 % |
| JasperFx | 0.0 % |
| Weasel.Core | 0.0 % |

## Methoden selbst (exklusiv)

| Methode | Anteil |
|---|---:|
| `Marten.Linq.Parsing.LinqInternalExtensions.ReduceToConstant` | 58.2 % |
| `System.Reflection.Emit.DynamicMethod.GetMethodDescriptor` | 7.2 % |
| `Interop+Sys.Send` | 3.6 % |
| `System.Collections.Generic.Dictionary`2[System.__Canon,System.__Canon].Resize` | 2.4 % |
| `?` | 2.0 % |
| `Infrastructure.Persistence.MartenEventStore+<Materialisiere>d__13.MoveNext` | 1.9 % |
| `System.Collections.Generic.List`1[System.__Canon].set_Capacity` | 1.9 % |
| `System.Guid.NewGuid` | 1.7 % |
| `Marten.Events.EventMapping+<>c__DisplayClass50_0+<<JsonTransformation>b__2>d.MoveNext` | 1.1 % |
| `System.Diagnostics.Metrics.CounterAggregator.Update` | 1.0 % |
| `System.Security.Cryptography.MD5.HashData` | 1.0 % |
| `FastExpressionCompiler.ExpressionCompiler.TryCompileBoundToFirstClosureParam` | 0.9 % |
| `Marten.Linq.MartenLinqQueryProvider+<ExecuteHandlerAsync>d__21`1[System.__Canon].MoveNext` | 0.8 % |
| `Interop+Sys.Receive` | 0.8 % |
| `Npgsql.Util.ResettableCancellationTokenSource.Start` | 0.7 % |
| `System.Linq.Enumerable+ArrayWhereIterator`1[System.__Canon].ToList` | 0.6 % |
| `System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.__Canon].GetStateMachineBox` | 0.6 % |
| `Marten.Linq.Parsing.SimpleExpression..ctor` | 0.6 % |
| `Npgsql.Internal.NpgsqlConnector.ResetCancellation` | 0.5 % |
| `System.Delegate.GetMethodImpl` | 0.5 % |
| `Interop+Kernel32.SetEvent` | 0.5 % |
| `Infrastructure.Persistence.MartenEventStore+<ReadStreamAsync>d__10.MoveNext` | 0.5 % |
| `System.Security.Cryptography.SHA256.HashData` | 0.5 % |
| `Interop+Kernel32.ReleaseSemaphore` | 0.4 % |
| `System.Net.Sockets.SocketAsyncContext+OperationQueue`1[System.__Canon].ProcessSyncEventOrGetAsyncEvent` | 0.4 % |
| `…em.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Threading.Tasks.VoidTaskResult].GetStateMachineBox` | 0.4 % |
| `Infrastructure.Persistence.MartenEventStore+<ReadEventAsync>d__12.MoveNext` | 0.4 % |
| `System.Linq.Expressions.Expression.NewArrayInit` | 0.3 % |
| `System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[Polly.Outcome`1[System.__Canon]].GetStateMachineBox` | 0.3 % |
| `Proto.CancellationTokens.FromSeconds` | 0.3 % |

## Methoden gesamt (inklusiv, ohne Laufzeit-Rahmen)

| Methode | Anteil |
|---|---:|
| `Marten.Linq.MartenLinqQueryable`1+<ToListAsync>d__36`1[System.__Canon,System.__Canon].MoveNext` | 79.1 % |
| `Marten.Linq.Parsing.LinqQueryParser.BuildStatements` | 68.9 % |
| `Marten.Linq.Parsing.LinqQueryParser.BuildListHandler` | 68.9 % |
| `Marten.Linq.CollectionUsage.BuildTopStatement` | 68.7 % |
| `Marten.Linq.SqlGeneration.Statement.ParseWhereClause` | 68.6 % |
| `Marten.Linq.Parsing.WhereClauseParser.Visit` | 68.5 % |
| `Marten.Linq.Parsing.WhereClauseParser.VisitBinary` | 68.5 % |
| `Marten.Linq.Parsing.LinqInternalExtensions.ReduceToConstant` | 67.9 % |
| `Proto.Mailbox.DefaultMailbox.RunAsync` | 60.2 % |
| `Proto.Mailbox.DefaultMailbox.Execute` | 60.2 % |
| `Proto.Mailbox.DefaultMailbox.ProcessMessages` | 60.2 % |
| `Marten.Linq.Parsing.SimpleExpression..ctor` | 56.6 % |
| `Proto.Middleware.Receive` | 51.4 % |
| `Proto.Cluster.Extensions+<>c__DisplayClass6_1.<WithClusterKind>b__4` | 51.4 % |
| `Infrastructure.Prozess.ProzessManagerActor+<ReceiveAsync>d__14.MoveNext` | 49.8 % |
| `Infrastructure.Prozess.ProzessManagerActor.ReceiveAsync` | 49.7 % |
| `Infrastructure.Prozess.ProzessManager+<WakeAsync>d__22.MoveNext` | 44.3 % |
| `Infrastructure.Prozess.ProzessManager+<FalteAsync>d__30.MoveNext` | 43.4 % |
| `Infrastructure.Prozess.ProzessManager+<FaltMarkingMitCursorAsync>d__29.MoveNext` | 42.9 % |
| `Infrastructure.Persistence.MartenEventStore+<ReadKorreliertAsync>d__11.MoveNext` | 42.7 % |
| `Infrastructure.Prozess.ProzessManager+<>c__DisplayClass30_0+<<FalteAsync>g__Integriere|0>d.MoveNext` | 42.5 % |
| `…ystem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 36.8 % |
| `…xecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 36.3 % |
| `Npgsql.NpgsqlCommand+<ExecuteReader>d__120.MoveNext` | 35.8 % |
| `Polly.ResiliencePipeline+<>c__9`2+<<ExecuteAsync>b__9_0>d[System.__Canon,System.__Canon].MoveNext` | 34.8 % |
| `Polly.ResiliencePipeline+<ExecuteAsync>d__9`2[System.__Canon,System.__Canon].MoveNext` | 34.6 % |
| `Npgsql.Internal.NpgsqlConnector+<ReadMessageLong>d__235.MoveNext` | 33.1 % |
| `…stem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]]].MoveNext` | 32.9 % |
| `…ecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 32.8 % |
| `Npgsql.Internal.NpgsqlReadBuffer+<<Ensure>g__EnsureLong|55_0>d.MoveNext` | 32.7 % |
| `Npgsql.NpgsqlDataReader+<NextResult>d__52.MoveNext` | 32.2 % |
| `…ueTaskAsync|4_0>d`2[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 32.1 % |
| `…eTaskAsync|4_0>d`2[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 32.1 % |
| `Infrastructure.Persistence.MartenEventStore+<ReadStreamAsync>d__10.MoveNext` | 31.7 % |
| `…TaskMethodBuilder`1+AsyncStateMachineBox`1[System.__Canon,Npgsql.NpgsqlCommand+<ExecuteReader>d__120].MoveNext` | 31.7 % |
| `…cTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Boolean,Npgsql.NpgsqlDataReader+<NextResult>d__52].MoveNext` | 31.6 % |
| `…em.__Canon],Polly.ResiliencePipeline+<>c__9`2+<<ExecuteAsync>b__9_0>d[System.__Canon,System.__Canon]].MoveNext` | 31.6 % |
| `…ineBox`1[System.__Canon,Polly.ResiliencePipeline+<ExecuteAsync>d__9`2[System.__Canon,System.__Canon]].MoveNext` | 30.8 % |
| `Marten.Linq.Parsing.SimpleExpression.VisitMember` | 26.7 % |
| `Marten.Linq.Parsing.SimpleExpression.VisitUnary` | 26.7 % |
| `Marten.Linq.MartenLinqQueryProvider+<ExecuteHandlerAsync>d__21`1[System.__Canon].MoveNext` | 23.1 % |
| `Marten.Internal.Sessions.AutoClosingLifetime+<ExecuteReaderAsync>d__14.MoveNext` | 22.4 % |
| `…teMachineBox`1[System.__Canon,Marten.Internal.Sessions.AutoClosingLifetime+<ExecuteReaderAsync>d__14].MoveNext` | 19.1 % |
| `…ox`1[System.__Canon,Marten.Linq.MartenLinqQueryProvider+<ExecuteHandlerAsync>d__21`1[System.__Canon]].MoveNext` | 18.3 % |
| `Infrastructure.Prozess.ProzessManager+<StarteAsync>d__21.MoveNext` | 15.7 % |
