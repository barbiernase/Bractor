# CPU-Profil des Hosts

Stichproben 32307 (≈ 10 ms je Thread), davon arbeitend (innerster Rahmen keine Warte-Methode) 16116 ≈ 161.2 Thread-Sekunden; Markierung: UNMANAGED_CODE_TIME 32284, CPU_TIME 23

Die Anteile beziehen sich auf die arbeitenden Stichproben.

## Arbeit nach Bibliothek (innerster Rahmen außerhalb der Laufzeit)

| Bibliothek | Anteil |
|---|---:|
| Infrastructure | 51.5 % |
| Marten | 25.7 % |
| Npgsql | 14.8 % |
| Laufzeit allein | 4.0 % |
| Proto.Actor | 1.7 % |
| Weasel.Postgresql | 0.8 % |
| Polly.Core | 0.6 % |
| Abstractions | 0.4 % |
| Proto.Cluster | 0.3 % |
| JasperFx.Events | 0.1 % |
| Pruefdomaene.Laufzeit | 0.0 % |
| Consul | 0.0 % |

## Methoden selbst (exklusiv)

| Methode | Anteil |
|---|---:|
| `Infrastructure.Persistence.MartenEventStore+<Materialisiere>d__21.MoveNext` | 41.2 % |
| `System.Text.Json.ReadStack.Push` | 17.3 % |
| `System.Text.Json.ReadStack.Pop` | 7.3 % |
| `Interop+Sys.Receive` | 4.4 % |
| `Interop+Sys.Send` | 3.4 % |
| `?` | 2.4 % |
| `System.Linq.Enumerable+IteratorSelectIterator`2[System.__Canon,System.__Canon].ToArrayNoPresizing` | 2.4 % |
| `Interop+Kernel32.ReleaseSemaphore` | 1.6 % |
| `System.Diagnostics.Metrics.CounterAggregator.Update` | 1.5 % |
| `Npgsql.Internal.NpgsqlConnector.ResetCancellation` | 1.3 % |
| `System.Linq.Enumerable+ArrayWhereIterator`1[System.__Canon].ToList` | 1.1 % |
| `System.Collections.Generic.List`1[System.__Canon].set_Capacity` | 1.0 % |
| `Npgsql.Util.ResettableCancellationTokenSource.Start` | 1.0 % |
| `System.Net.Sockets.SocketAsyncContext+OperationQueue`1[System.__Canon].ProcessSyncEventOrGetAsyncEvent` | 0.8 % |
| `Proto.Mailbox.LockingUnboundedMailboxQueue.Push` | 0.7 % |
| `Npgsql.Util.ResettableCancellationTokenSource.Stop` | 0.7 % |
| `Npgsql.NpgsqlConnection.FromDataSource` | 0.6 % |
| `System.Linq.Enumerable+OrderedIterator`2[System.__Canon,System.Int32].TryGetFirst` | 0.6 % |
| `Npgsql.BackendMessages.RowDescriptionMessage.LoadColumnInfoCache` | 0.5 % |
| `System.String.CreateStringFromEncoding` | 0.5 % |
| `Infrastructure.Aggregate.EventEnvelopeFactory.BuildPerEvent` | 0.4 % |
| `System.Security.Cryptography.MD5.HashData` | 0.4 % |
| `System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.__Canon].GetStateMachineBox` | 0.4 % |
| `System.Threading.SemaphoreSlim.Release` | 0.4 % |
| `…em.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Threading.Tasks.VoidTaskResult].GetStateMachineBox` | 0.4 % |
| `Infrastructure.Persistence.MartenEventBatchWriter.Stage` | 0.4 % |
| `Infrastructure.Persistence.MartenEventStore+<LeseEventsAsync>d__14.MoveNext` | 0.3 % |
| `System.Threading.Channels.UnboundedChannel`1+UnboundedChannelWriter[System.__Canon].TryWrite` | 0.3 % |
| `System.Collections.Generic.List`1[System.__Canon].AddRange` | 0.3 % |
| `Proto.Mailbox.LockingUnboundedMailboxQueue.Pop` | 0.3 % |

## Methoden gesamt (inklusiv, ohne Laufzeit-Rahmen)

| Methode | Anteil |
|---|---:|
| `Npgsql.Internal.NpgsqlReadBuffer+<<Ensure>g__EnsureLong|55_0>d.MoveNext` | 80.1 % |
| `Npgsql.Internal.NpgsqlConnector+<ReadMessageLong>d__235.MoveNext` | 79.8 % |
| `Npgsql.NpgsqlCommand+<ExecuteReader>d__120.MoveNext` | 76.6 % |
| `Infrastructure.Persistence.MartenEventStore+<LeseEventsAsync>d__14.MoveNext` | 74.8 % |
| `Npgsql.NpgsqlDataReader+<NextResult>d__52.MoveNext` | 73.6 % |
| `…cTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Boolean,Npgsql.NpgsqlDataReader+<NextResult>d__52].MoveNext` | 73.0 % |
| `…le`2[System.__Canon,System.Int32],Infrastructure.Persistence.MartenEventStore+<LeseEventsAsync>d__14].MoveNext` | 71.7 % |
| `Infrastructure.Persistence.MartenEventStore.LeseZeile` | 67.0 % |
| `Infrastructure.Persistence.MartenEventStore+<Materialisiere>d__21.MoveNext` | 41.4 % |
| `Marten.Services.SystemTextJsonSerializer.FromJson` | 24.8 % |
| `…tem.Text.Json.Serialization.Converters.ObjectWithParameterizedConstructorConverter`1[System.__Canon].OnTryRead` | 24.7 % |
| `…rter`5[System.__Canon,System.Int32,System.Int32,System.__Canon,System.__Canon].ReadAndCacheConstructorArgument` | 13.2 % |
| `…eterizedConstructorConverter`5[System.__Canon,System.Int32,System.Int32,System.__Canon,System.__Canon].TryRead` | 13.2 % |
| `…er`5[System.__Canon,System.Int32,System.__Canon,System.__Canon,System.__Canon].ReadAndCacheConstructorArgument` | 11.1 % |
| `…erizedConstructorConverter`5[System.__Canon,System.Int32,System.__Canon,System.__Canon,System.__Canon].TryRead` | 11.1 % |
| `Proto.Mailbox.DefaultMailbox.RunAsync` | 11.1 % |
| `Proto.Mailbox.DefaultMailbox.ProcessMessages` | 11.1 % |
| `Proto.Mailbox.DefaultMailbox.Execute` | 11.1 % |
| `Proto.Context.ActorContext.InvokeSystemMessageAsync` | 9.1 % |
| `Proto.Context.ActorContext.HandleContinuation` | 9.1 % |
| `Proto.Context.ActorContext+<HandleContinuation>d__81.MoveNext` | 9.1 % |
| `Infrastructure.Lebenszyklus.RahmenActor+<>c__DisplayClass24_0+<<Neben>b__0>d.MoveNext` | 8.6 % |
| `Npgsql.NpgsqlDataReader+<Read>d__48.MoveNext` | 8.0 % |
| `Infrastructure.Persistence.MartenEventStore+<GesetztAsync>d__25.MoveNext` | 6.4 % |
| `Infrastructure.Funktionen.FunktionsActor.NachLesen` | 6.3 % |
| `Infrastructure.Persistence.MartenEventStore+<ReadPartitionAsync>d__24.MoveNext` | 6.0 % |
| `Infrastructure.Persistence.MartenEventBatchWriter+<WriteBatchAsync>d__4.MoveNext` | 5.7 % |
| `Infrastructure.Lebenszyklus.RahmenActor.Neben` | 5.6 % |
| `…stem.Threading.Tasks.VoidTaskResult,Infrastructure.Persistence.BatchingEventAppender+<RunAsync>d__19].MoveNext` | 5.1 % |
| `Infrastructure.Persistence.BatchingEventAppender+<RunAsync>d__19.MoveNext` | 5.1 % |
| `Infrastructure.Persistence.BatchingEventAppender+<FlushAsync>d__20.MoveNext` | 5.0 % |
| `Marten.Internal.Sessions.DocumentSessionBase+<SaveChangesAsync>d__66.MoveNext` | 5.0 % |
| `Marten.Internal.Sessions.AutoClosingLifetime+<ExecuteBatchPagesAsync>d__16.MoveNext` | 4.9 % |
| `Marten.Internal.Sessions.DocumentSessionBase+<ExecuteBatchAsync>d__74.MoveNext` | 4.6 % |
| `Interop+Sys.Receive` | 4.4 % |
| `…s.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Boolean,Npgsql.NpgsqlDataReader+<Read>d__48].MoveNext` | 4.2 % |
| `Npgsql.Internal.NpgsqlConnector.ReadMessageLong` | 4.0 % |
| `Npgsql.NpgsqlDataReader.Read` | 4.0 % |
| `…ystem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 3.9 % |
| `…Tuple`2[System.Int64,System.Boolean],Infrastructure.Persistence.MartenEventStore+<GesetztAsync>d__25].MoveNext` | 3.8 % |
| `Npgsql.NpgsqlCommand+<<Write>g__WriteExecute|101_0>d.MoveNext` | 3.8 % |
| `Npgsql.Internal.NpgsqlWriteBuffer+<Flush>d__38.MoveNext` | 3.8 % |
| `Polly.ResiliencePipeline+<ExecuteAsync>d__2`1[System.__Canon].MoveNext` | 3.8 % |
| `Npgsql.Internal.NpgsqlReadBuffer.Ensure` | 3.7 % |
| `…xecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 3.6 % |
