# CPU-Profil des Hosts

Stichproben 497899 (≈ 10 ms je Thread), davon arbeitend (innerster Rahmen keine Warte-Methode) 292902 ≈ 2929.0 Thread-Sekunden; Markierung: UNMANAGED_CODE_TIME 497866, CPU_TIME 33

Die Anteile beziehen sich auf die arbeitenden Stichproben.

## Arbeit nach Bibliothek (innerster Rahmen außerhalb der Laufzeit)

| Bibliothek | Anteil |
|---|---:|
| Npgsql | 33.6 % |
| Infrastructure | 21.8 % |
| Abstractions | 14.3 % |
| Laufzeit allein | 7.6 % |
| Proto.Actor | 6.3 % |
| StackExchange.Redis | 5.8 % |
| Proto.Cluster | 3.1 % |
| Marten | 2.2 % |
| Pipelines.Sockets.Unofficial | 2.0 % |
| Polly.Core | 1.3 % |
| Weasel.Postgresql | 1.0 % |
| JasperFx.Events | 0.4 % |
| Anonymously Hosted DynamicMethods Assembly | 0.2 % |
| JasperFx | 0.1 % |
| JasperFx.RuntimeCompiler | 0.0 % |
| ghaijbod.j5o | 0.0 % |
| Pruefdomaene | 0.0 % |
| 1ikiu0ez.pme | 0.0 % |
| Weasel.Core | 0.0 % |
| Pruefdomaene.Laufzeit | 0.0 % |

## Methoden selbst (exklusiv)

| Methode | Anteil |
|---|---:|
| `Interop+Sys.Send` | 16.9 % |
| `Interop+Kernel32.SetEvent` | 4.7 % |
| `System.Security.Cryptography.MD5.HashData` | 4.3 % |
| `System.Linq.Enumerable+ArrayWhereIterator`1[System.__Canon].ToList` | 3.8 % |
| `?` | 3.6 % |
| `System.Guid.NewGuid` | 2.6 % |
| `System.Diagnostics.Metrics.CounterAggregator.Update` | 2.5 % |
| `Infrastructure.Persistence.MartenEventStore+<Materialisiere>d__21.MoveNext` | 2.2 % |
| `Interop+Sys.Receive` | 2.1 % |
| `System.Security.Cryptography.SHA256.HashData` | 2.0 % |
| `System.Security.Cryptography.HashProviderDispenser+OneShotHashProvider.HashData` | 1.9 % |
| `System.Threading.Lock.TryEnterSlow` | 1.8 % |
| `Npgsql.Internal.NpgsqlConnector.ResetCancellation` | 1.8 % |
| `System.Threading.SpinWait.SpinOnceCore` | 1.7 % |
| `System.Collections.Generic.List`1[System.__Canon].set_Capacity` | 1.6 % |
| `System.Threading.Channels.UnboundedChannel`1+UnboundedChannelWriter[System.__Canon].TryWrite` | 1.5 % |
| `System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.__Canon].GetStateMachineBox` | 1.4 % |
| `Proto.CancellationTokens.FromSeconds` | 1.4 % |
| `System.Net.Sockets.SocketAsyncContext+OperationQueue`1[System.__Canon].ProcessSyncEventOrGetAsyncEvent` | 1.2 % |
| `System.Collections.Concurrent.ConcurrentDictionary`2[System.Guid,System.__Canon].TryAddInternal` | 1.2 % |
| `System.Collections.Generic.List`1[System.__Canon]..ctor` | 1.1 % |
| `…em.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Threading.Tasks.VoidTaskResult].GetStateMachineBox` | 1.1 % |
| `System.Diagnostics.Debugger.NotifyOfCrossThreadDependency` | 1.1 % |
| `Npgsql.NpgsqlConnection.FromDataSource` | 1.0 % |
| `System.Collections.Generic.SegmentedArrayBuilder`1[System.__Canon].ToList` | 1.0 % |
| `Pipelines.Sockets.Unofficial.DedicatedThreadPoolPipeScheduler.Schedule` | 0.9 % |
| `System.Threading.CancellationTokenSource.InitializeWithTimer` | 0.9 % |
| `System.Threading.SemaphoreSlim.Release` | 0.8 % |
| `Npgsql.NpgsqlDataReader+<NextResult>d__52.MoveNext` | 0.8 % |
| `Npgsql.NpgsqlCommand..ctor` | 0.8 % |

## Methoden gesamt (inklusiv, ohne Laufzeit-Rahmen)

| Methode | Anteil |
|---|---:|
| `Npgsql.NpgsqlCommand+<ExecuteReader>d__120.MoveNext` | 42.1 % |
| `Proto.Mailbox.DefaultMailbox.Execute` | 42.0 % |
| `Proto.Mailbox.DefaultMailbox.RunAsync` | 42.0 % |
| `Proto.Mailbox.DefaultMailbox.ProcessMessages` | 41.9 % |
| `Npgsql.Internal.NpgsqlReadBuffer+<<Ensure>g__EnsureLong|55_0>d.MoveNext` | 27.1 % |
| `Npgsql.Internal.NpgsqlConnector+<ReadMessageLong>d__235.MoveNext` | 26.7 % |
| `Proto.Cluster.Extensions+<>c__DisplayClass6_1.<WithClusterKind>b__4` | 26.5 % |
| `Proto.Middleware.Receive` | 26.5 % |
| `Npgsql.NpgsqlDataReader+<NextResult>d__52.MoveNext` | 24.1 % |
| `Infrastructure.Prozess.ProzessManager+<WakeAsync>d__22.MoveNext` | 23.2 % |
| `…cTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Boolean,Npgsql.NpgsqlDataReader+<NextResult>d__52].MoveNext` | 22.1 % |
| `Infrastructure.Prozess.ProzessManager+<FalteAsync>d__30.MoveNext` | 21.6 % |
| `…TaskMethodBuilder`1+AsyncStateMachineBox`1[System.__Canon,Npgsql.NpgsqlCommand+<ExecuteReader>d__120].MoveNext` | 21.6 % |
| `Infrastructure.Prozess.ProzessManagerActor+<ReceiveAsync>d__14.MoveNext` | 21.5 % |
| `Infrastructure.Prozess.ProzessManagerActor.ReceiveAsync` | 21.3 % |
| `Infrastructure.Prozess.ProzessManager+<FaltMarkingMitCursorAsync>d__29.MoveNext` | 20.4 % |
| `Infrastructure.Persistence.MartenEventStore+<LeseEventsAsync>d__14.MoveNext` | 19.0 % |
| `Infrastructure.Persistence.MartenEventStore+<ReadKorreliertAsync>d__11.MoveNext` | 18.3 % |
| `Npgsql.NpgsqlCommand+<<Write>g__WriteExecute|101_0>d.MoveNext` | 18.0 % |
| `Npgsql.Internal.NpgsqlWriteBuffer+<Flush>d__38.MoveNext` | 16.9 % |
| `Interop+Sys.Send` | 16.9 % |
| `Infrastructure.Prozess.ProzessManager+Einarbeitung+<IntegriereAsync>d__15.MoveNext` | 14.0 % |
| `…ystem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 13.9 % |
| `Infrastructure.Persistence.MartenEventStore+<LeseStreamAsync>d__13.MoveNext` | 13.7 % |
| `…xecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 13.4 % |
| `…Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Threading.Tasks.VoidTaskResult].SetExistingTaskResult` | 13.0 % |
| `…teMachineBox`1[System.__Canon,Infrastructure.Persistence.MartenEventStore+<ReadKorreliertAsync>d__11].MoveNext` | 10.8 % |
| `Infrastructure.Aggregate.ActorSystem.AggregateActorBase`1+<AbschliessenAsync>d__37[System.__Canon].MoveNext` | 10.7 % |
| `…te.ActorSystem.AggregateActorBase`1+<>c__DisplayClass34_1+<<StarteBuendelAsync>b__2>d[System.__Canon].MoveNext` | 10.7 % |
| `…le`2[System.__Canon,System.Int32],Infrastructure.Persistence.MartenEventStore+<LeseEventsAsync>d__14].MoveNext` | 10.4 % |
| `Proto.Context.ActorContext.InvokeSystemMessageAsync` | 10.4 % |
| `Proto.Context.ActorContext+<HandleContinuation>d__81.MoveNext` | 10.1 % |
| `Infrastructure.Aggregate.ActorSystem.AggregateActorBase`1[System.__Canon].AbschliessenAsync` | 10.0 % |
| `Proto.Context.ActorContext.Receive` | 9.0 % |
| `…ueTaskAsync|4_0>d`2[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 8.8 % |
| `…eTaskAsync|4_0>d`2[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 8.6 % |
| `…stem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]]].MoveNext` | 8.5 % |
| `Proto.Cluster.DefaultClusterContext+<RequestAsync>d__9`1[System.__Canon].MoveNext` | 8.5 % |
| `…ecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 8.4 % |
| `Marten.Internal.Sessions.AutoClosingLifetime+<ExecuteBatchPagesAsync>d__16.MoveNext` | 8.2 % |
| `Marten.Internal.Sessions.DocumentSessionBase+<SaveChangesAsync>d__66.MoveNext` | 7.8 % |
| `Marten.Internal.Sessions.DocumentSessionBase+<ExecuteBatchAsync>d__74.MoveNext` | 7.6 % |
| `Infrastructure.Prozess.ProzessManager.WakeAsync` | 7.5 % |
| `Polly.ResiliencePipeline+<ExecuteAsync>d__2`1[System.__Canon].MoveNext` | 7.4 % |
| `Npgsql.NpgsqlCommand.ExecuteReaderAsync` | 7.3 % |
