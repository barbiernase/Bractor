# CPU-Profil des Hosts

Stichproben 26756 (≈ 10 ms je Thread), davon arbeitend (innerster Rahmen keine Warte-Methode) 11863 ≈ 118.6 Thread-Sekunden; Markierung: UNMANAGED_CODE_TIME 26725, CPU_TIME 31

Die Anteile beziehen sich auf die arbeitenden Stichproben.

## Arbeit nach Bibliothek (innerster Rahmen außerhalb der Laufzeit)

| Bibliothek | Anteil |
|---|---:|
| Npgsql | 29.4 % |
| Infrastructure | 21.8 % |
| Abstractions | 12.9 % |
| Marten | 10.6 % |
| Laufzeit allein | 7.7 % |
| Proto.Actor | 5.2 % |
| StackExchange.Redis | 4.0 % |
| Proto.Cluster | 2.4 % |
| Pipelines.Sockets.Unofficial | 2.4 % |
| Polly.Core | 1.5 % |
| Weasel.Postgresql | 1.1 % |
| JasperFx.Events | 0.6 % |
| Anonymously Hosted DynamicMethods Assembly | 0.2 % |
| JasperFx | 0.2 % |
| Pruefdomaene | 0.0 % |
| Pruefdomaene.Laufzeit | 0.0 % |
| t1pf5hhj.mtp | 0.0 % |
| Pruefdomaene.Projections | 0.0 % |
| Consul | 0.0 % |
| Newtonsoft.Json | 0.0 % |

## Methoden selbst (exklusiv)

| Methode | Anteil |
|---|---:|
| `Interop+Sys.Send` | 15.5 % |
| `System.Collections.Generic.Dictionary`2[System.__Canon,System.__Canon].Resize` | 8.3 % |
| `Infrastructure.Persistence.MartenEventStore+<Materialisiere>d__21.MoveNext` | 5.4 % |
| `System.Security.Cryptography.MD5.HashData` | 4.8 % |
| `?` | 3.5 % |
| `System.Linq.Enumerable+ArrayWhereIterator`1[System.__Canon].ToList` | 3.0 % |
| `System.Diagnostics.Metrics.CounterAggregator.Update` | 2.9 % |
| `Interop+Sys.Receive` | 2.7 % |
| `System.Guid.NewGuid` | 2.7 % |
| `Interop+Kernel32.SetEvent` | 2.4 % |
| `System.Security.Cryptography.SHA256.HashData` | 2.1 % |
| `Npgsql.Util.ResettableCancellationTokenSource.Start` | 2.0 % |
| `System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.__Canon].GetStateMachineBox` | 1.7 % |
| `Interop+Kernel32.ReleaseSemaphore` | 1.7 % |
| `Npgsql.Internal.NpgsqlConnector.ResetCancellation` | 1.5 % |
| `System.Collections.Generic.List`1[System.__Canon].set_Capacity` | 1.4 % |
| `…em.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Threading.Tasks.VoidTaskResult].GetStateMachineBox` | 1.4 % |
| `System.Net.Sockets.SocketAsyncContext+OperationQueue`1[System.__Canon].ProcessSyncEventOrGetAsyncEvent` | 1.3 % |
| `System.Threading.Channels.UnboundedChannel`1+UnboundedChannelWriter[System.__Canon].TryWrite` | 1.1 % |
| `Proto.CancellationTokens.FromSeconds` | 1.1 % |
| `Npgsql.NpgsqlConnection.FromDataSource` | 1.1 % |
| `System.Collections.Generic.List`1[System.__Canon]..ctor` | 1.0 % |
| `System.DateTime.get_UtcNow` | 1.0 % |
| `System.Collections.Concurrent.ConcurrentDictionary`2[System.Guid,System.__Canon].TryAddInternal` | 1.0 % |
| `System.Diagnostics.Debugger.NotifyOfCrossThreadDependency` | 0.9 % |
| `System.Threading.CancellationTokenSource.InitializeWithTimer` | 0.8 % |
| `Infrastructure.Prozess.DetachedProzessSend.SendeAsync` | 0.8 % |
| `System.Span`1[System.__Canon].CopyTo` | 0.8 % |
| `Infrastructure.Persistence.MartenEventStore+<LeseStreamAsync>d__13.MoveNext` | 0.8 % |
| `System.Collections.Generic.SegmentedArrayBuilder`1[System.__Canon].ToList` | 0.8 % |

## Methoden gesamt (inklusiv, ohne Laufzeit-Rahmen)

| Methode | Anteil |
|---|---:|
| `Npgsql.NpgsqlCommand+<ExecuteReader>d__120.MoveNext` | 49.6 % |
| `Infrastructure.Persistence.MartenEventStore+<LeseStreamAsync>d__13.MoveNext` | 43.2 % |
| `Npgsql.Internal.NpgsqlReadBuffer+<<Ensure>g__EnsureLong|55_0>d.MoveNext` | 38.6 % |
| `Npgsql.Internal.NpgsqlConnector+<ReadMessageLong>d__235.MoveNext` | 38.6 % |
| `Proto.Mailbox.DefaultMailbox.Execute` | 36.7 % |
| `Proto.Mailbox.DefaultMailbox.RunAsync` | 36.7 % |
| `Proto.Mailbox.DefaultMailbox.ProcessMessages` | 36.6 % |
| `Npgsql.NpgsqlDataReader+<NextResult>d__52.MoveNext` | 34.4 % |
| `…cTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Boolean,Npgsql.NpgsqlDataReader+<NextResult>d__52].MoveNext` | 32.8 % |
| `…TaskMethodBuilder`1+AsyncStateMachineBox`1[System.__Canon,Npgsql.NpgsqlCommand+<ExecuteReader>d__120].MoveNext` | 32.4 % |
| `…cStateMachineBox`1[System.__Canon,Infrastructure.Persistence.MartenEventStore+<LeseStreamAsync>d__13].MoveNext` | 29.1 % |
| `Proto.Middleware.Receive` | 23.3 % |
| `Proto.Cluster.Extensions+<>c__DisplayClass6_1.<WithClusterKind>b__4` | 23.3 % |
| `Infrastructure.Prozess.ProzessManager+<WakeAsync>d__22.MoveNext` | 19.3 % |
| `Infrastructure.Prozess.ProzessManager+<FalteAsync>d__30.MoveNext` | 17.8 % |
| `Infrastructure.Prozess.ProzessManagerActor+<ReceiveAsync>d__14.MoveNext` | 17.4 % |
| `Infrastructure.Prozess.ProzessManagerActor.ReceiveAsync` | 17.4 % |
| `Infrastructure.Prozess.ProzessManager+<FaltMarkingMitCursorAsync>d__29.MoveNext` | 16.5 % |
| `Npgsql.Internal.NpgsqlWriteBuffer+<Flush>d__38.MoveNext` | 16.2 % |
| `Npgsql.NpgsqlCommand+<<Write>g__WriteExecute|101_0>d.MoveNext` | 15.8 % |
| `Interop+Sys.Send` | 15.5 % |
| `…ystem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 14.5 % |
| `…xecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 13.9 % |
| `Infrastructure.Prozess.ProzessManager+<>c__DisplayClass30_0+<<FalteAsync>g__Integriere|0>d.MoveNext` | 12.0 % |
| `…stem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]]].MoveNext` | 9.6 % |
| `Marten.Internal.Sessions.AutoClosingLifetime+<ExecuteBatchPagesAsync>d__16.MoveNext` | 9.4 % |
| `…ecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 9.4 % |
| `…ueTaskAsync|4_0>d`2[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 9.4 % |
| `…eTaskAsync|4_0>d`2[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 9.3 % |
| `Marten.Services.SystemTextJsonSerializer.FromJson` | 8.6 % |
| `…Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Threading.Tasks.VoidTaskResult].SetExistingTaskResult` | 8.3 % |
| `…stem.Text.Json.Serialization.JsonDictionaryConverter`3[System.__Canon,System.__Canon,System.__Canon].OnTryRead` | 8.3 % |
| `Proto.Context.ActorContext.InvokeSystemMessageAsync` | 8.3 % |
| `Marten.Internal.Sessions.DocumentSessionBase+<SaveChangesAsync>d__66.MoveNext` | 8.2 % |
| `Infrastructure.Aggregate.ActorSystem.AggregateActorBase`1+<AbschliessenAsync>d__37[System.__Canon].MoveNext` | 8.2 % |
| `Marten.Internal.Sessions.DocumentSessionBase+<ExecuteBatchAsync>d__74.MoveNext` | 8.1 % |
| `…te.ActorSystem.AggregateActorBase`1+<>c__DisplayClass34_1+<<StarteBuendelAsync>b__2>d[System.__Canon].MoveNext` | 8.1 % |
| `Proto.Context.ActorContext+<HandleContinuation>d__81.MoveNext` | 8.0 % |
| `Polly.ResiliencePipeline+<ExecuteAsync>d__2`1[System.__Canon].MoveNext` | 7.8 % |
| `Infrastructure.Aggregate.ActorSystem.AggregateActorBase`1[System.__Canon].AbschliessenAsync` | 7.8 % |
| `Polly.ResiliencePipeline+<>c__2`1+<<ExecuteAsync>b__2_0>d[System.__Canon].MoveNext` | 7.6 % |
| `Polly.Utils.Pipeline.BridgeComponent`1[System.__Canon].ExecuteCore` | 7.3 % |
| `Polly.Utils.Pipeline.CompositeComponent.ExecuteCoreWithoutTelemetry` | 7.3 % |
| `Polly.Retry.RetryResilienceStrategy`1[System.__Canon].ExecuteCore` | 7.3 % |
| `…ding.Tasks.VoidTaskResult,Marten.Internal.Sessions.AutoClosingLifetime+<ExecuteBatchPagesAsync>d__16].MoveNext` | 7.1 % |
