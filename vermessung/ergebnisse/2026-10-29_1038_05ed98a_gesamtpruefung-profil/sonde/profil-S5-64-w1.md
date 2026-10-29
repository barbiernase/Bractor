# CPU-Profil des Hosts

Stichproben 30493 (≈ 10 ms je Thread), davon arbeitend (innerster Rahmen keine Warte-Methode) 14627 ≈ 146.3 Thread-Sekunden; Markierung: UNMANAGED_CODE_TIME 30471, CPU_TIME 22

Die Anteile beziehen sich auf die arbeitenden Stichproben.

## Arbeit nach Bibliothek (innerster Rahmen außerhalb der Laufzeit)

| Bibliothek | Anteil |
|---|---:|
| Infrastructure | 51.4 % |
| Marten | 25.1 % |
| Npgsql | 15.2 % |
| Laufzeit allein | 4.3 % |
| Proto.Actor | 1.7 % |
| Polly.Core | 0.6 % |
| Weasel.Postgresql | 0.6 % |
| Abstractions | 0.5 % |
| Proto.Cluster | 0.4 % |
| JasperFx.Events | 0.1 % |
| Pruefdomaene.Laufzeit | 0.0 % |
| Consul | 0.0 % |
| Proto.Remote | 0.0 % |
| Google.Protobuf | 0.0 % |

## Methoden selbst (exklusiv)

| Methode | Anteil |
|---|---:|
| `Infrastructure.Persistence.MartenEventStore+<Materialisiere>d__21.MoveNext` | 41.2 % |
| `System.Text.Json.ReadStack.Push` | 17.0 % |
| `System.Text.Json.ReadStack.Pop` | 7.0 % |
| `Interop+Sys.Receive` | 4.1 % |
| `Interop+Sys.Send` | 3.6 % |
| `?` | 2.6 % |
| `System.Linq.Enumerable+IteratorSelectIterator`2[System.__Canon,System.__Canon].ToArrayNoPresizing` | 2.3 % |
| `Interop+Kernel32.ReleaseSemaphore` | 1.8 % |
| `System.Diagnostics.Metrics.CounterAggregator.Update` | 1.7 % |
| `Npgsql.Internal.NpgsqlConnector.ResetCancellation` | 1.5 % |
| `System.Linq.Enumerable+ArrayWhereIterator`1[System.__Canon].ToList` | 1.3 % |
| `Npgsql.Util.ResettableCancellationTokenSource.Start` | 1.0 % |
| `System.Net.Sockets.SocketAsyncContext+OperationQueue`1[System.__Canon].ProcessSyncEventOrGetAsyncEvent` | 0.8 % |
| `System.Collections.Generic.List`1[System.__Canon].set_Capacity` | 0.7 % |
| `Proto.Mailbox.LockingUnboundedMailboxQueue.Push` | 0.7 % |
| `Npgsql.NpgsqlConnection.FromDataSource` | 0.7 % |
| `Npgsql.Util.ResettableCancellationTokenSource.Stop` | 0.6 % |
| `System.Linq.Enumerable+OrderedIterator`2[System.__Canon,System.Int32].TryGetFirst` | 0.5 % |
| `System.Security.Cryptography.MD5.HashData` | 0.5 % |
| `Npgsql.BackendMessages.RowDescriptionMessage.LoadColumnInfoCache` | 0.5 % |
| `Infrastructure.Persistence.MartenEventStore+<LeseEventsAsync>d__14.MoveNext` | 0.4 % |
| `System.Threading.SemaphoreSlim.Release` | 0.4 % |
| `System.Threading.Channels.UnboundedChannel`1+UnboundedChannelWriter[System.__Canon].TryWrite` | 0.4 % |
| `System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.__Canon].GetStateMachineBox` | 0.4 % |
| `System.String.CreateStringFromEncoding` | 0.4 % |
| `…tor`2[Abstractions.OneOf`4[System.__Canon,System.__Canon,System.__Canon,System.__Canon],System.__Canon].ToList` | 0.4 % |
| `System.Collections.Generic.List`1[System.__Canon].AddRange` | 0.4 % |
| `…em.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Threading.Tasks.VoidTaskResult].GetStateMachineBox` | 0.4 % |
| `Infrastructure.Persistence.MartenEventBatchWriter.Stage` | 0.4 % |
| `Infrastructure.Aggregate.EventEnvelopeFactory.BuildPerEvent` | 0.3 % |

## Methoden gesamt (inklusiv, ohne Laufzeit-Rahmen)

| Methode | Anteil |
|---|---:|
| `Npgsql.NpgsqlCommand+<ExecuteReader>d__120.MoveNext` | 79.8 % |
| `Npgsql.Internal.NpgsqlReadBuffer+<<Ensure>g__EnsureLong|55_0>d.MoveNext` | 78.9 % |
| `Npgsql.Internal.NpgsqlConnector+<ReadMessageLong>d__235.MoveNext` | 78.5 % |
| `Npgsql.NpgsqlDataReader+<NextResult>d__52.MoveNext` | 75.9 % |
| `…cTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Boolean,Npgsql.NpgsqlDataReader+<NextResult>d__52].MoveNext` | 75.4 % |
| `…TaskMethodBuilder`1+AsyncStateMachineBox`1[System.__Canon,Npgsql.NpgsqlCommand+<ExecuteReader>d__120].MoveNext` | 75.0 % |
| `Infrastructure.Persistence.MartenEventStore+<LeseEventsAsync>d__14.MoveNext` | 73.9 % |
| `…le`2[System.__Canon,System.Int32],Infrastructure.Persistence.MartenEventStore+<LeseEventsAsync>d__14].MoveNext` | 70.7 % |
| `Infrastructure.Persistence.MartenEventStore.LeseZeile` | 66.1 % |
| `Infrastructure.Persistence.MartenEventStore+<Materialisiere>d__21.MoveNext` | 41.3 % |
| `Marten.Services.SystemTextJsonSerializer.FromJson` | 24.2 % |
| `…tem.Text.Json.Serialization.Converters.ObjectWithParameterizedConstructorConverter`1[System.__Canon].OnTryRead` | 24.1 % |
| `…eterizedConstructorConverter`5[System.__Canon,System.Int32,System.Int32,System.__Canon,System.__Canon].TryRead` | 13.0 % |
| `…rter`5[System.__Canon,System.Int32,System.Int32,System.__Canon,System.__Canon].ReadAndCacheConstructorArgument` | 13.0 % |
| `Proto.Mailbox.DefaultMailbox.RunAsync` | 11.6 % |
| `Proto.Mailbox.DefaultMailbox.Execute` | 11.6 % |
| `Proto.Mailbox.DefaultMailbox.ProcessMessages` | 11.6 % |
| `…erizedConstructorConverter`5[System.__Canon,System.Int32,System.__Canon,System.__Canon,System.__Canon].TryRead` | 10.9 % |
| `…er`5[System.__Canon,System.Int32,System.__Canon,System.__Canon,System.__Canon].ReadAndCacheConstructorArgument` | 10.9 % |
| `Proto.Context.ActorContext.InvokeSystemMessageAsync` | 9.5 % |
| `Proto.Context.ActorContext+<HandleContinuation>d__81.MoveNext` | 9.5 % |
| `Proto.Context.ActorContext.HandleContinuation` | 9.5 % |
| `Infrastructure.Lebenszyklus.RahmenActor+<>c__DisplayClass24_0+<<Neben>b__0>d.MoveNext` | 9.1 % |
| `Infrastructure.Persistence.MartenEventStore+<GesetztAsync>d__25.MoveNext` | 6.8 % |
| `Infrastructure.Funktionen.FunktionsActor.NachLesen` | 6.6 % |
| `Infrastructure.Persistence.MartenEventStore+<ReadPartitionAsync>d__24.MoveNext` | 6.5 % |
| `Infrastructure.Lebenszyklus.RahmenActor.Neben` | 6.3 % |
| `Infrastructure.Persistence.MartenEventBatchWriter+<WriteBatchAsync>d__4.MoveNext` | 5.9 % |
| `Marten.Internal.Sessions.DocumentSessionBase+<SaveChangesAsync>d__66.MoveNext` | 5.3 % |
| `…stem.Threading.Tasks.VoidTaskResult,Infrastructure.Persistence.BatchingEventAppender+<RunAsync>d__19].MoveNext` | 5.2 % |
| `Infrastructure.Persistence.BatchingEventAppender+<RunAsync>d__19.MoveNext` | 5.2 % |
| `Infrastructure.Persistence.BatchingEventAppender+<FlushAsync>d__20.MoveNext` | 5.2 % |
| `Marten.Internal.Sessions.AutoClosingLifetime+<ExecuteBatchPagesAsync>d__16.MoveNext` | 5.0 % |
| `Marten.Internal.Sessions.DocumentSessionBase+<ExecuteBatchAsync>d__74.MoveNext` | 5.0 % |
| `Npgsql.NpgsqlCommand+<<Write>g__WriteExecute|101_0>d.MoveNext` | 4.4 % |
| `Npgsql.NpgsqlDataReader+<Read>d__48.MoveNext` | 4.3 % |
| `…ystem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 4.1 % |
| `Interop+Sys.Receive` | 4.1 % |
| `Polly.ResiliencePipeline+<ExecuteAsync>d__2`1[System.__Canon].MoveNext` | 4.0 % |
| `Npgsql.Internal.NpgsqlWriteBuffer+<Flush>d__38.MoveNext` | 4.0 % |
| `Npgsql.Internal.NpgsqlConnector.ReadMessageLong` | 3.9 % |
| `…xecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 3.9 % |
| `…Tuple`2[System.Int64,System.Boolean],Infrastructure.Persistence.MartenEventStore+<GesetztAsync>d__25].MoveNext` | 3.9 % |
| `Polly.ResiliencePipeline+<>c__2`1+<<ExecuteAsync>b__2_0>d[System.__Canon].MoveNext` | 3.8 % |
| `Npgsql.NpgsqlDataReader.Read` | 3.8 % |
