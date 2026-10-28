# CPU-Profil des Hosts

Stichproben 42466 (≈ 10 ms je Thread), davon arbeitend (innerster Rahmen keine Warte-Methode) 23283 ≈ 232.8 Thread-Sekunden; Markierung: UNMANAGED_CODE_TIME 42439, CPU_TIME 27

Die Anteile beziehen sich auf die arbeitenden Stichproben.

## Arbeit nach Bibliothek (innerster Rahmen außerhalb der Laufzeit)

| Bibliothek | Anteil |
|---|---:|
| Abstractions | 30.5 % |
| Marten | 25.3 % |
| Npgsql | 18.8 % |
| Infrastructure | 15.1 % |
| Laufzeit allein | 5.2 % |
| Proto.Actor | 2.6 % |
| Polly.Core | 1.1 % |
| JasperFx.Events | 0.6 % |
| Proto.Cluster | 0.5 % |
| Weasel.Postgresql | 0.1 % |
| Anonymously Hosted DynamicMethods Assembly | 0.1 % |
| Pruefdomaene.Laufzeit | 0.0 % |
| JasperFx | 0.0 % |
| Proto.Remote | 0.0 % |
| Consul | 0.0 % |

## Methoden selbst (exklusiv)

| Methode | Anteil |
|---|---:|
| `System.Guid.NewGuid` | 29.6 % |
| `System.Text.Json.ReadStack.Push` | 16.9 % |
| `System.Text.Json.ReadStack.Pop` | 6.7 % |
| `Infrastructure.Persistence.MartenEventStore+<Materialisiere>d__21.MoveNext` | 4.0 % |
| `Interop+Sys.Send` | 3.9 % |
| `Interop+Sys.Receive` | 2.7 % |
| `Npgsql.Internal.NpgsqlConnector.ResetCancellation` | 2.5 % |
| `?` | 2.0 % |
| `System.Diagnostics.Metrics.CounterAggregator.Update` | 2.0 % |
| `Interop+Kernel32.ReleaseSemaphore` | 1.8 % |
| `System.Collections.Generic.SegmentedArrayBuilder`1[System.__Canon].ToList` | 1.7 % |
| `System.DateTime.get_UtcNow` | 1.7 % |
| `System.Diagnostics.Stopwatch.GetTimestamp` | 1.7 % |
| `System.Collections.Generic.SegmentedArrayBuilder`1[System.__Canon].ToArray` | 1.6 % |
| `System.Diagnostics.Debugger.NotifyOfCrossThreadDependency` | 1.4 % |
| `Proto.Mailbox.LockingUnboundedMailboxQueue.Push` | 1.1 % |
| `Npgsql.Util.ResettableCancellationTokenSource.Start` | 1.0 % |
| `System.Collections.Generic.List`1[System.__Canon].set_Capacity` | 1.0 % |
| `Npgsql.NpgsqlConnection..ctor` | 0.9 % |
| `System.Net.Sockets.SocketAsyncContext+OperationQueue`1[System.__Canon].ProcessSyncEventOrGetAsyncEvent` | 0.7 % |
| `Npgsql.NpgsqlCommand..ctor` | 0.7 % |
| `System.Threading.Channels.UnboundedChannel`1+UnboundedChannelWriter[System.__Canon].TryWrite` | 0.6 % |
| `System.TimeProvider.GetTimestamp` | 0.6 % |
| `System.Buffer.Memmove` | 0.6 % |
| `System.Linq.Enumerable+IteratorSelectIterator`2[System.__Canon,System.__Canon].ToArrayNoPresizing` | 0.5 % |
| `System.Buffer._Memmove` | 0.5 % |
| `System.Linq.Enumerable+ArrayWhereIterator`1[System.__Canon].ToList` | 0.5 % |
| `System.Linq.Enumerable.ToArray` | 0.5 % |
| `System.Linq.Enumerable+OrderedIterator`2[System.__Canon,System.Int32].TryGetFirst` | 0.4 % |
| `Npgsql.Util.ResettableCancellationTokenSource.Stop` | 0.4 % |

## Methoden gesamt (inklusiv, ohne Laufzeit-Rahmen)

| Methode | Anteil |
|---|---:|
| `Npgsql.NpgsqlCommand+<ExecuteReader>d__120.MoveNext` | 74.7 % |
| `Npgsql.Internal.NpgsqlReadBuffer+<<Ensure>g__EnsureLong|55_0>d.MoveNext` | 70.7 % |
| `Npgsql.Internal.NpgsqlConnector+<ReadMessageLong>d__235.MoveNext` | 70.5 % |
| `Npgsql.NpgsqlDataReader+<NextResult>d__52.MoveNext` | 69.2 % |
| `…cTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Boolean,Npgsql.NpgsqlDataReader+<NextResult>d__52].MoveNext` | 68.5 % |
| `…TaskMethodBuilder`1+AsyncStateMachineBox`1[System.__Canon,Npgsql.NpgsqlCommand+<ExecuteReader>d__120].MoveNext` | 68.0 % |
| `Infrastructure.Persistence.MartenEventStore+<LeseEventsAsync>d__14.MoveNext` | 65.9 % |
| `…le`2[System.__Canon,System.Int32],Infrastructure.Persistence.MartenEventStore+<LeseEventsAsync>d__14].MoveNext` | 61.2 % |
| `Infrastructure.Persistence.MartenEventStore.LeseZeile` | 57.9 % |
| `…non,System.Int32],Infrastructure.Persistence.MartenEventStore+<LeseEventsAsync>d__14].ExecutionContextCallback` | 54.6 % |
| `…eMachineBox`1[System.__Canon,Npgsql.Internal.NpgsqlConnector+<ReadMessageLong>d__235].ExecutionContextCallback` | 48.2 % |
| `….Tasks.VoidTaskResult,Npgsql.Internal.NpgsqlReadBuffer+<<Ensure>g__EnsureLong|55_0>d].ExecutionContextCallback` | 48.1 % |
| `…odBuilder`1+StateMachineBox`1[System.__Canon,Npgsql.Internal.NpgsqlConnector+<ReadMessageLong>d__235].MoveNext` | 48.1 % |
| `…er`1+AsyncStateMachineBox`1[System.Boolean,Npgsql.NpgsqlDataReader+<NextResult>d__52].ExecutionContextCallback` | 47.3 % |
| `…r`1+AsyncStateMachineBox`1[System.__Canon,Npgsql.NpgsqlCommand+<ExecuteReader>d__120].ExecutionContextCallback` | 47.1 % |
| `Infrastructure.Persistence.MartenEventStore+<Materialisiere>d__21.MoveNext` | 33.6 % |
| `Abstractions.EventEnvelope..ctor` | 29.8 % |
| `Marten.Services.SystemTextJsonSerializer.FromJson` | 23.8 % |
| `…tem.Text.Json.Serialization.Converters.ObjectWithParameterizedConstructorConverter`1[System.__Canon].OnTryRead` | 23.8 % |
| `Proto.Mailbox.DefaultMailbox.RunAsync` | 14.4 % |
| `Proto.Mailbox.DefaultMailbox.Execute` | 14.4 % |
| `Proto.Mailbox.DefaultMailbox.ProcessMessages` | 14.4 % |
| `…eterizedConstructorConverter`5[System.__Canon,System.Int32,System.Int32,System.__Canon,System.__Canon].TryRead` | 12.8 % |
| `…rter`5[System.__Canon,System.Int32,System.Int32,System.__Canon,System.__Canon].ReadAndCacheConstructorArgument` | 12.8 % |
| `Proto.Context.ActorContext.InvokeSystemMessageAsync` | 11.2 % |
| `Proto.Context.ActorContext+<HandleContinuation>d__81.MoveNext` | 11.1 % |
| `…er`5[System.__Canon,System.Int32,System.__Canon,System.__Canon,System.__Canon].ReadAndCacheConstructorArgument` | 10.6 % |
| `…erizedConstructorConverter`5[System.__Canon,System.Int32,System.__Canon,System.__Canon,System.__Canon].TryRead` | 10.6 % |
| `Infrastructure.Lebenszyklus.RahmenActor+<>c__DisplayClass24_0+<<Neben>b__0>d.MoveNext` | 10.0 % |
| `Infrastructure.Lebenszyklus.RahmenActor+<>c__DisplayClass24_0.<Neben>b__0` | 10.0 % |
| `Npgsql.NpgsqlCommand.ExecuteReaderAsync` | 8.8 % |
| `Infrastructure.Persistence.MartenEventStore+<GesetztAsync>d__25.MoveNext` | 8.7 % |
| `Infrastructure.Persistence.MartenEventBatchWriter+<WriteBatchAsync>d__4.MoveNext` | 8.6 % |
| `Infrastructure.Persistence.MartenEventStore+<ReadPartitionAsync>d__24.MoveNext` | 8.0 % |
| `Marten.Internal.Sessions.DocumentSessionBase+<SaveChangesAsync>d__66.MoveNext` | 7.8 % |
| `…stem.Threading.Tasks.VoidTaskResult,Infrastructure.Persistence.BatchingEventAppender+<RunAsync>d__19].MoveNext` | 7.8 % |
| `Infrastructure.Persistence.BatchingEventAppender+<RunAsync>d__19.MoveNext` | 7.8 % |
| `Infrastructure.Persistence.BatchingEventAppender+<FlushAsync>d__20.MoveNext` | 7.7 % |
| `Npgsql.NpgsqlCommand.ExecuteReader` | 7.6 % |
| `…ystem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 7.4 % |
| `Marten.Internal.Sessions.DocumentSessionBase+<ExecuteBatchAsync>d__74.MoveNext` | 7.3 % |
| `Infrastructure.Funktionen.FunktionsActor.NachLesen` | 7.1 % |
| `…asks.VoidTaskResult,Infrastructure.Persistence.BatchingEventAppender+<RunAsync>d__19].ExecutionContextCallback` | 7.0 % |
| `Infrastructure.Persistence.MartenEventBatchWriter.WriteBatchAsync` | 6.9 % |
| `…xecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 6.9 % |
