# CPU-Profil des Hosts

Stichproben 90029 (≈ 10 ms je Thread), davon arbeitend (innerster Rahmen keine Warte-Methode) 37847 ≈ 378.5 Thread-Sekunden; Markierung: UNMANAGED_CODE_TIME 90024, CPU_TIME 5

Die Anteile beziehen sich auf die arbeitenden Stichproben.

## Arbeit nach Bibliothek (innerster Rahmen außerhalb der Laufzeit)

| Bibliothek | Anteil |
|---|---:|
| Npgsql | 31.7 % |
| Laufzeit allein | 23.0 % |
| Infrastructure | 16.3 % |
| Marten | 7.7 % |
| Grpc.AspNetCore.Server | 7.0 % |
| Google.Protobuf | 4.1 % |
| Cqrs.Leitung | 2.7 % |
| Domain.Laufzeit | 2.4 % |
| Polly.Core | 2.2 % |
| Proto.Actor | 1.4 % |
| Weasel.Postgresql | 0.6 % |
| Proto.Cluster | 0.3 % |
| Domain.Infrastructure | 0.2 % |
| JasperFx.Events | 0.1 % |
| Core | 0.1 % |
| 1lk5jx2d.14d | 0.1 % |
| Domain.Projections | 0.0 % |
| Domain.Leitung | 0.0 % |
| Domain | 0.0 % |
| JasperFx | 0.0 % |

## Methoden selbst (exklusiv)

| Methode | Anteil |
|---|---:|
| `Interop+Sys.Send` | 23.6 % |
| `Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol.get_RequestAborted` | 5.2 % |
| `System.Diagnostics.Metrics.CounterAggregator.Update` | 3.6 % |
| `?` | 3.6 % |
| `System.Collections.Generic.Dictionary`2[System.Guid,System.__Canon].FindValue` | 3.2 % |
| `Interop+Sys.Receive` | 3.0 % |
| `Npgsql.Internal.NpgsqlConnector.ResetCancellation` | 2.9 % |
| `Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http2.Http2OutputProducer.Advance` | 2.9 % |
| `dynamicClass.ResolveService` | 2.1 % |
| `Interop+Kernel32.ReleaseSemaphore` | 1.9 % |
| `System.Collections.Generic.Dictionary`2[System.__Canon,System.__Canon].Resize` | 1.6 % |
| `Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http2.Http2OutputProducer.GetSpan` | 1.6 % |
| `System.Text.Json.ReadStack.Push` | 1.6 % |
| `System.IO.Pipelines.Pipe.GetReadAsyncResult` | 1.5 % |
| `System.Threading.Channels.BoundedChannel`1+BoundedChannelReader[System.__Canon].TryRead` | 1.3 % |
| `System.Buffer._Memmove` | 1.3 % |
| `System.IO.Pipelines.Pipe.AdvanceReader` | 1.2 % |
| `System.Net.Sockets.SocketAsyncContext+OperationQueue`1[System.__Canon].ProcessSyncEventOrGetAsyncEvent` | 1.2 % |
| `Npgsql.BackendMessages.RowDescriptionMessage.LoadColumnInfoCache` | 1.1 % |
| `System.IO.Pipelines.Pipe.Advance` | 1.0 % |
| `System.Text.Json.ReadStack.Pop` | 0.9 % |
| `Npgsql.NpgsqlConnection.FromDataSource` | 0.9 % |
| `System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.__Canon].GetStateMachineBox` | 0.9 % |
| `System.Collections.Generic.List`1[System.__Canon].set_Capacity` | 0.9 % |
| `System.Text.Json.WriteStack.Push` | 0.8 % |
| `System.IO.Pipelines.Pipe.AllocateWriteHeadSynchronized` | 0.8 % |
| `System.Guid.NewGuid` | 0.7 % |
| `Npgsql.Util.ResettableCancellationTokenSource.Start` | 0.7 % |
| `Npgsql.Util.ResettableCancellationTokenSource.Stop` | 0.7 % |
| `Infrastructure.Persistence.MartenEventStore+<Materialisiere>d__21.MoveNext` | 0.7 % |

## Methoden gesamt (inklusiv, ohne Laufzeit-Rahmen)

| Methode | Anteil |
|---|---:|
| `Npgsql.NpgsqlCommand+<ExecuteReader>d__120.MoveNext` | 42.3 % |
| `Npgsql.Internal.NpgsqlReadBuffer+<<Ensure>g__EnsureLong|55_0>d.MoveNext` | 34.5 % |
| `Npgsql.Internal.NpgsqlConnector+<ReadMessageLong>d__235.MoveNext` | 34.0 % |
| `Npgsql.NpgsqlDataReader+<NextResult>d__52.MoveNext` | 33.2 % |
| `Proto.Mailbox.DefaultMailbox.Execute` | 32.4 % |
| `Proto.Mailbox.DefaultMailbox.RunAsync` | 32.3 % |
| `Proto.Mailbox.DefaultMailbox.ProcessMessages` | 32.3 % |
| `…cTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Boolean,Npgsql.NpgsqlDataReader+<NextResult>d__52].MoveNext` | 32.1 % |
| `…TaskMethodBuilder`1+AsyncStateMachineBox`1[System.__Canon,Npgsql.NpgsqlCommand+<ExecuteReader>d__120].MoveNext` | 31.6 % |
| `…ystem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 28.6 % |
| `Infrastructure.Lebenszyklus.RahmenActor.ReceiveAsync` | 28.3 % |
| `Infrastructure.Lebenszyklus.RahmenActor+<ReceiveAsync>d__17.MoveNext` | 28.3 % |
| `…xecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 28.1 % |
| `Polly.ResiliencePipeline+<>c__9`2+<<ExecuteAsync>b__9_0>d[System.__Canon,System.__Canon].MoveNext` | 24.3 % |
| `Marten.Internal.Sessions.AutoClosingLifetime+<ExecuteReaderAsync>d__12.MoveNext` | 24.3 % |
| `Polly.ResiliencePipeline+<ExecuteAsync>d__9`2[System.__Canon,System.__Canon].MoveNext` | 24.1 % |
| `Interop+Sys.Send` | 23.6 % |
| `…ecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 23.2 % |
| `…stem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]]].MoveNext` | 23.2 % |
| `…ueTaskAsync|4_0>d`2[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 22.5 % |
| `…eTaskAsync|4_0>d`2[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 22.4 % |
| `Marten.Internal.Sessions.QuerySession+<LoadOneAsync>d__78`1[System.__Canon].MoveNext` | 21.4 % |
| `…teMachineBox`1[System.__Canon,Marten.Internal.Sessions.AutoClosingLifetime+<ExecuteReaderAsync>d__12].MoveNext` | 20.9 % |
| `…em.__Canon],Polly.ResiliencePipeline+<>c__9`2+<<ExecuteAsync>b__9_0>d[System.__Canon,System.__Canon]].MoveNext` | 20.9 % |
| `…ineBox`1[System.__Canon,Polly.ResiliencePipeline+<ExecuteAsync>d__9`2[System.__Canon,System.__Canon]].MoveNext` | 20.3 % |
| `Infrastructure.GrpcClient.SitzungsActor+<BehandleAsync>d__26.MoveNext` | 19.9 % |
| `Infrastructure.GrpcClient.SitzungsActor.BehandleAsync` | 19.9 % |
| `Infrastructure.Projections.ProjektionsStaende+<StaendeAsync>d__10.MoveNext` | 18.9 % |
| `Marten.Internal.Sessions.QuerySession+<LoadAsync>d__106`1[System.__Canon].MoveNext` | 18.3 % |
| `…hineBox`1[System.__Canon,Marten.Internal.Sessions.QuerySession+<LoadOneAsync>d__78`1[System.__Canon]].MoveNext` | 17.5 % |
| `Infrastructure.Projections.ProjektionsStaende+<StaendeAsync>d__9.MoveNext` | 17.4 % |
| `Npgsql.NpgsqlCommand+<<Write>g__WriteExecute|101_0>d.MoveNext` | 17.1 % |
| `Npgsql.Internal.NpgsqlWriteBuffer+<Flush>d__38.MoveNext` | 16.3 % |
| `Infrastructure.Projections.Generated.GeneratedQueryDienst+<FrageAsync>d__33.MoveNext` | 15.3 % |
| `…achineBox`1[System.__Canon,Marten.Internal.Sessions.QuerySession+<LoadAsync>d__106`1[System.__Canon]].MoveNext` | 13.7 % |
| `Infrastructure.GrpcClient.SitzungsActor+<SchreibeAsync>d__43.MoveNext` | 12.2 % |
| `Grpc.AspNetCore.Server.Internal.HttpContextStreamWriter`1+<WriteCoreAsync>d__14[System.__Canon].MoveNext` | 12.2 % |
| `Infrastructure.Projections.ProjektionsStaende+<WarteAsync>d__8.MoveNext` | 12.0 % |
| `Polly.Utils.Pipeline.BridgeComponent`1[System.__Canon].ExecuteCore` | 11.6 % |
| `Polly.Utils.Pipeline.CompositeComponent.ExecuteCoreWithoutTelemetry` | 11.6 % |
| `Polly.Retry.RetryResilienceStrategy`1[System.__Canon].ExecuteCore` | 11.4 % |
| `…soft.AspNetCore.Server.Kestrel.Transport.Sockets.Internal.IOQueue.System.Threading.IThreadPoolWorkItem.Execute` | 10.5 % |
| `…Result,Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.Internal.SocketConnection+<DoSend>d__28].MoveNext` | 10.4 % |
| `…Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Threading.Tasks.VoidTaskResult].SetExistingTaskResult` | 9.7 % |
| `Core.MartenStore+<>c__DisplayClass10_0`1+<<EnqueueTransform>b__0>d[System.__Canon].MoveNext` | 9.4 % |
