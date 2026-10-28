# CPU-Profil des Hosts

Stichproben 46086 (≈ 10 ms je Thread), davon arbeitend (innerster Rahmen keine Warte-Methode) 24108 ≈ 241.1 Thread-Sekunden; Markierung: UNMANAGED_CODE_TIME 46052, CPU_TIME 34

Die Anteile beziehen sich auf die arbeitenden Stichproben.

## Arbeit nach Bibliothek (innerster Rahmen außerhalb der Laufzeit)

| Bibliothek | Anteil |
|---|---:|
| Abstractions | 30.8 % |
| Marten | 24.8 % |
| Npgsql | 17.7 % |
| Infrastructure | 14.2 % |
| Laufzeit allein | 4.4 % |
| Proto.Actor | 2.7 % |
| Pipelines.Sockets.Unofficial | 1.7 % |
| StackExchange.Redis | 1.0 % |
| Polly.Core | 1.0 % |
| JasperFx.Events | 0.6 % |
| Proto.Cluster | 0.6 % |
| Anonymously Hosted DynamicMethods Assembly | 0.2 % |
| Weasel.Postgresql | 0.1 % |
| Pruefdomaene.Laufzeit | 0.0 % |
| JasperFx | 0.0 % |
| Pruefdomaene.Pipeline | 0.0 % |
| jtapcjf0.lxt | 0.0 % |
| FastExpressionCompiler | 0.0 % |
| Google.Protobuf | 0.0 % |
| Consul | 0.0 % |

## Methoden selbst (exklusiv)

| Methode | Anteil |
|---|---:|
| `System.Guid.NewGuid` | 29.9 % |
| `System.Text.Json.ReadStack.Push` | 16.5 % |
| `System.Text.Json.ReadStack.Pop` | 6.6 % |
| `Interop+Sys.Send` | 3.8 % |
| `Infrastructure.Persistence.MartenEventStore+<Materialisiere>d__20.MoveNext` | 2.7 % |
| `Interop+Sys.Receive` | 2.6 % |
| `Npgsql.Internal.NpgsqlConnector.ResetCancellation` | 2.4 % |
| `System.DateTime.get_UtcNow` | 2.2 % |
| `System.Diagnostics.Metrics.CounterAggregator.Update` | 2.0 % |
| `?` | 1.9 % |
| `System.Collections.Generic.SegmentedArrayBuilder`1[System.__Canon].ToArray` | 1.8 % |
| `System.Collections.Generic.SegmentedArrayBuilder`1[System.__Canon].ToList` | 1.7 % |
| `Interop+Kernel32.ReleaseSemaphore` | 1.5 % |
| `System.Diagnostics.Stopwatch.GetTimestamp` | 1.5 % |
| `System.Diagnostics.Debugger.NotifyOfCrossThreadDependency` | 1.4 % |
| `System.Collections.Generic.List`1[System.__Canon].set_Capacity` | 1.1 % |
| `Proto.Mailbox.LockingUnboundedMailboxQueue.Push` | 1.0 % |
| `Npgsql.Util.ResettableCancellationTokenSource.Start` | 0.9 % |
| `Npgsql.NpgsqlConnection..ctor` | 0.8 % |
| `System.Net.Sockets.SocketAsyncContext+OperationQueue`1[System.__Canon].ProcessSyncEventOrGetAsyncEvent` | 0.7 % |
| `Npgsql.NpgsqlCommand..ctor` | 0.7 % |
| `System.Threading.Channels.UnboundedChannel`1+UnboundedChannelWriter[System.__Canon].TryWrite` | 0.6 % |
| `System.Buffer._Memmove` | 0.6 % |
| `Npgsql.Util.ResettableCancellationTokenSource.Stop` | 0.6 % |
| `System.Buffer.Memmove` | 0.5 % |
| `System.TimeProvider.GetTimestamp` | 0.5 % |
| `System.Linq.Enumerable+OrderedIterator`2[System.__Canon,System.Int32].TryGetFirst` | 0.5 % |
| `System.Collections.Generic.List`1[System.__Canon].AddRange` | 0.5 % |
| `System.Linq.Enumerable.ToArray` | 0.4 % |
| `Proto.CancellationTokens.FromSeconds` | 0.4 % |

## Methoden gesamt (inklusiv, ohne Laufzeit-Rahmen)

| Methode | Anteil |
|---|---:|
| `Npgsql.NpgsqlCommand+<ExecuteReader>d__120.MoveNext` | 71.8 % |
| `Npgsql.Internal.NpgsqlReadBuffer+<<Ensure>g__EnsureLong|55_0>d.MoveNext` | 68.3 % |
| `Npgsql.Internal.NpgsqlConnector+<ReadMessageLong>d__235.MoveNext` | 67.8 % |
| `Npgsql.NpgsqlDataReader+<NextResult>d__52.MoveNext` | 67.0 % |
| `…cTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Boolean,Npgsql.NpgsqlDataReader+<NextResult>d__52].MoveNext` | 66.4 % |
| `…TaskMethodBuilder`1+AsyncStateMachineBox`1[System.__Canon,Npgsql.NpgsqlCommand+<ExecuteReader>d__120].MoveNext` | 65.9 % |
| `Infrastructure.Persistence.MartenEventStore+<LeseEventsAsync>d__13.MoveNext` | 64.1 % |
| `…le`2[System.__Canon,System.Int32],Infrastructure.Persistence.MartenEventStore+<LeseEventsAsync>d__13].MoveNext` | 59.6 % |
| `Infrastructure.Persistence.MartenEventStore.LeseZeile` | 56.4 % |
| `…non,System.Int32],Infrastructure.Persistence.MartenEventStore+<LeseEventsAsync>d__13].ExecutionContextCallback` | 55.1 % |
| `….Tasks.VoidTaskResult,Npgsql.Internal.NpgsqlReadBuffer+<<Ensure>g__EnsureLong|55_0>d].ExecutionContextCallback` | 47.8 % |
| `…System.Threading.Tasks.VoidTaskResult,Npgsql.Internal.NpgsqlReadBuffer+<<Ensure>g__EnsureLong|55_0>d].MoveNext` | 47.7 % |
| `…odBuilder`1+StateMachineBox`1[System.__Canon,Npgsql.Internal.NpgsqlConnector+<ReadMessageLong>d__235].MoveNext` | 47.5 % |
| `…eMachineBox`1[System.__Canon,Npgsql.Internal.NpgsqlConnector+<ReadMessageLong>d__235].ExecutionContextCallback` | 47.5 % |
| `…er`1+AsyncStateMachineBox`1[System.Boolean,Npgsql.NpgsqlDataReader+<NextResult>d__52].ExecutionContextCallback` | 46.9 % |
| `…r`1+AsyncStateMachineBox`1[System.__Canon,Npgsql.NpgsqlCommand+<ExecuteReader>d__120].ExecutionContextCallback` | 46.6 % |
| `Infrastructure.Persistence.MartenEventStore+<Materialisiere>d__20.MoveNext` | 32.6 % |
| `Abstractions.EventEnvelope..ctor` | 30.1 % |
| `Marten.Services.SystemTextJsonSerializer.FromJson` | 23.3 % |
| `…tem.Text.Json.Serialization.Converters.ObjectWithParameterizedConstructorConverter`1[System.__Canon].OnTryRead` | 23.2 % |
| `Proto.Mailbox.DefaultMailbox.Execute` | 16.1 % |
| `Proto.Mailbox.DefaultMailbox.RunAsync` | 16.1 % |
| `Proto.Mailbox.DefaultMailbox.ProcessMessages` | 16.0 % |
| `…eterizedConstructorConverter`5[System.__Canon,System.Int32,System.Int32,System.__Canon,System.__Canon].TryRead` | 12.6 % |
| `…rter`5[System.__Canon,System.Int32,System.Int32,System.__Canon,System.__Canon].ReadAndCacheConstructorArgument` | 12.6 % |
| `Proto.Context.ActorContext.InvokeSystemMessageAsync` | 12.5 % |
| `Proto.Context.ActorContext+<HandleContinuation>d__81.MoveNext` | 12.5 % |
| `…er`5[System.__Canon,System.Int32,System.__Canon,System.__Canon,System.__Canon].ReadAndCacheConstructorArgument` | 10.5 % |
| `…erizedConstructorConverter`5[System.__Canon,System.Int32,System.__Canon,System.__Canon,System.__Canon].TryRead` | 10.5 % |
| `Infrastructure.Lebenszyklus.RahmenActor+<>c__DisplayClass24_0.<Neben>b__0` | 9.8 % |
| `Infrastructure.Lebenszyklus.RahmenActor+<>c__DisplayClass24_0+<<Neben>b__0>d.MoveNext` | 9.8 % |
| `Infrastructure.Persistence.MartenEventStore+<GesetztAsync>d__24.MoveNext` | 7.9 % |
| `Npgsql.NpgsqlCommand.ExecuteReaderAsync` | 7.9 % |
| `Infrastructure.Persistence.MartenEventBatchWriter+<WriteBatchAsync>d__3.MoveNext` | 7.8 % |
| `Infrastructure.Persistence.BatchingEventAppender+<RunAsync>d__19.MoveNext` | 7.6 % |
| `…stem.Threading.Tasks.VoidTaskResult,Infrastructure.Persistence.BatchingEventAppender+<RunAsync>d__19].MoveNext` | 7.6 % |
| `Infrastructure.Persistence.BatchingEventAppender+<FlushAsync>d__20.MoveNext` | 7.5 % |
| `Infrastructure.Persistence.MartenEventStore+<ReadPartitionAsync>d__23.MoveNext` | 7.3 % |
| `Infrastructure.Funktionen.FunktionsActor.NachLesen` | 7.3 % |
| `Marten.Internal.Sessions.DocumentSessionBase+<SaveChangesAsync>d__66.MoveNext` | 7.1 % |
| `…asks.VoidTaskResult,Infrastructure.Persistence.BatchingEventAppender+<RunAsync>d__19].ExecutionContextCallback` | 7.0 % |
| `Infrastructure.Persistence.MartenEventBatchWriter.WriteBatchAsync` | 6.8 % |
| `Infrastructure.Funktionen.FunktionsActor+<>c__DisplayClass62_0.<Lies>b__1` | 6.8 % |
| `Infrastructure.Persistence.BatchingEventAppender.FlushAsync` | 6.8 % |
| `Npgsql.NpgsqlCommand.ExecuteReader` | 6.7 % |
