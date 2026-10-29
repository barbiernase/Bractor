# CPU-Profil des Hosts

Stichproben 41303 (≈ 10 ms je Thread), davon arbeitend (innerster Rahmen keine Warte-Methode) 19012 ≈ 190.1 Thread-Sekunden; Markierung: UNMANAGED_CODE_TIME 41272, CPU_TIME 31

Die Anteile beziehen sich auf die arbeitenden Stichproben.

## Arbeit nach Bibliothek (innerster Rahmen außerhalb der Laufzeit)

| Bibliothek | Anteil |
|---|---:|
| Npgsql | 29.9 % |
| OpenCvSharp | 16.9 % |
| Laufzeit allein | 15.7 % |
| Infrastructure | 12.7 % |
| Marten | 7.8 % |
| Grpc.AspNetCore.Server | 3.7 % |
| Polly.Core | 2.4 % |
| Abstractions | 1.8 % |
| Google.Protobuf | 1.6 % |
| Proto.Actor | 1.4 % |
| JasperFx.RuntimeCompiler | 1.3 % |
| Domain.Laufzeit | 0.9 % |
| JasperFx.Events | 0.6 % |
| JasperFx | 0.6 % |
| Proto.Cluster | 0.4 % |
| Cqrs.Leitung | 0.4 % |
| Domain.Infrastructure | 0.3 % |
| Domain | 0.3 % |
| Weasel.Postgresql | 0.2 % |
| Domain.Leitung | 0.2 % |

## Methoden selbst (exklusiv)

| Methode | Anteil |
|---|---:|
| `Interop+Sys.Send` | 15.4 % |
| `OpenCvSharp.Internal.NativeMethods.imgcodecs_imwrite` | 6.2 % |
| `OpenCvSharp.Cv2.Resize` | 5.2 % |
| `?` | 4.7 % |
| `System.Diagnostics.Metrics.CounterAggregator.Update` | 3.6 % |
| `Npgsql.Internal.NpgsqlConnector.ResetCancellation` | 3.2 % |
| `dynamicClass.ResolveService` | 2.7 % |
| `Interop+Kernel32.ReleaseSemaphore` | 2.4 % |
| `Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol.get_RequestAborted` | 2.2 % |
| `System.Guid.NewGuid` | 2.2 % |
| `Interop+Sys.Receive` | 2.2 % |
| `OpenCvSharp.Cv2.CvtColor` | 2.0 % |
| `System.Diagnostics.Debugger.NotifyOfCrossThreadDependency` | 1.6 % |
| `OpenCvSharp.Cv2.EqualizeHist` | 1.5 % |
| `System.Text.Json.ReadStack.Push` | 1.3 % |
| `System.Text.Json.WriteStack.Push` | 1.2 % |
| `System.Text.Json.ReadStack.Pop` | 1.0 % |
| `System.DateTime.get_UtcNow` | 0.9 % |
| `System.Collections.Generic.List`1[System.__Canon].set_Capacity` | 0.9 % |
| `System.Net.Sockets.SocketAsyncContext+OperationQueue`1[System.__Canon].ProcessSyncEventOrGetAsyncEvent` | 0.9 % |
| `Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http2.Http2OutputProducer.Advance` | 0.9 % |
| `Npgsql.NpgsqlDataReader+<NextResult>d__52.MoveNext` | 0.9 % |
| `Npgsql.NpgsqlCommand..ctor` | 0.9 % |
| `System.IO.Pipelines.Pipe.AdvanceReader` | 0.9 % |
| `Marten.Storage.MartenDatabase.CreateConnection` | 0.8 % |
| `System.Collections.Generic.List`1[System.__Canon].AddRange` | 0.8 % |
| `System.IO.Pipelines.Pipe.GetReadAsyncResult` | 0.8 % |
| `Npgsql.Util.ResettableCancellationTokenSource.Start` | 0.8 % |
| `System.Collections.Generic.SegmentedArrayBuilder`1[System.__Canon].ToList` | 0.7 % |
| `System.Threading.Channels.UnboundedChannel`1+UnboundedChannelWriter[System.__Canon].TryWrite` | 0.7 % |

## Methoden gesamt (inklusiv, ohne Laufzeit-Rahmen)

| Methode | Anteil |
|---|---:|
| `Npgsql.NpgsqlCommand+<ExecuteReader>d__120.MoveNext` | 39.5 % |
| `…ystem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 28.8 % |
| `…xecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 27.9 % |
| `Npgsql.Internal.NpgsqlReadBuffer+<<Ensure>g__EnsureLong|55_0>d.MoveNext` | 26.6 % |
| `Npgsql.Internal.NpgsqlConnector+<ReadMessageLong>d__235.MoveNext` | 26.4 % |
| `Npgsql.NpgsqlDataReader+<NextResult>d__52.MoveNext` | 25.0 % |
| `…TaskMethodBuilder`1+AsyncStateMachineBox`1[System.__Canon,Npgsql.NpgsqlCommand+<ExecuteReader>d__120].MoveNext` | 24.0 % |
| `…cTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Boolean,Npgsql.NpgsqlDataReader+<NextResult>d__52].MoveNext` | 23.5 % |
| `Polly.ResiliencePipeline+<ExecuteAsync>d__9`2[System.__Canon,System.__Canon].MoveNext` | 23.4 % |
| `…ueTaskAsync|4_0>d`2[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 20.6 % |
| `…stem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]]].MoveNext` | 20.4 % |
| `…eTaskAsync|4_0>d`2[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 20.3 % |
| `Polly.ResiliencePipeline+<>c__9`2+<<ExecuteAsync>b__9_0>d[System.__Canon,System.__Canon].MoveNext` | 19.5 % |
| `Marten.Internal.Sessions.QuerySession+<LoadOneAsync>d__78`1[System.__Canon].MoveNext` | 19.3 % |
| `…ineBox`1[System.__Canon,Polly.ResiliencePipeline+<ExecuteAsync>d__9`2[System.__Canon,System.__Canon]].MoveNext` | 18.7 % |
| `…ecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 18.0 % |
| `Marten.Internal.Sessions.QuerySession+<LoadAsync>d__106`1[System.__Canon].MoveNext` | 17.9 % |
| `Marten.Internal.Sessions.AutoClosingLifetime+<ExecuteReaderAsync>d__12.MoveNext` | 16.9 % |
| `Interop+Sys.Send` | 15.4 % |
| `Infrastructure.Projections.Generated.GeneratedQueryDienst+<FrageAsync>d__33.MoveNext` | 15.4 % |
| `Proto.Mailbox.DefaultMailbox.RunAsync` | 15.3 % |
| `Proto.Mailbox.DefaultMailbox.Execute` | 15.3 % |
| `Proto.Mailbox.DefaultMailbox.ProcessMessages` | 15.3 % |
| `Npgsql.NpgsqlCommand.ExecuteReaderAsync` | 14.8 % |
| `…hineBox`1[System.__Canon,Marten.Internal.Sessions.QuerySession+<LoadOneAsync>d__78`1[System.__Canon]].MoveNext` | 14.7 % |
| `Npgsql.NpgsqlCommand+<<Write>g__WriteExecute|101_0>d.MoveNext` | 14.5 % |
| `Npgsql.Internal.NpgsqlWriteBuffer+<Flush>d__38.MoveNext` | 13.5 % |
| `Infrastructure.Projections.ProjektionsStaende+<StaendeAsync>d__10.MoveNext` | 13.3 % |
| `…em.__Canon],Polly.ResiliencePipeline+<>c__9`2+<<ExecuteAsync>b__9_0>d[System.__Canon,System.__Canon]].MoveNext` | 13.2 % |
| `Infrastructure.Projections.ProjektionsStaende+<StaendeAsync>d__9.MoveNext` | 12.3 % |
| `…achineBox`1[System.__Canon,Marten.Internal.Sessions.QuerySession+<LoadAsync>d__106`1[System.__Canon]].MoveNext` | 12.2 % |
| `…teMachineBox`1[System.__Canon,Marten.Internal.Sessions.AutoClosingLifetime+<ExecuteReaderAsync>d__12].MoveNext` | 11.7 % |
| `Polly.Utils.Pipeline.CompositeComponent.ExecuteCoreWithoutTelemetry` | 11.6 % |
| `Polly.Utils.Pipeline.BridgeComponent`1[System.__Canon].ExecuteCore` | 11.6 % |
| `Polly.Retry.RetryResilienceStrategy`1[System.__Canon].ExecuteCore` | 11.4 % |
| `Marten.Internal.Sessions.DocumentSessionBase+<SaveChangesAsync>d__66.MoveNext` | 11.4 % |
| `Infrastructure.Lebenszyklus.RahmenActor+<ReceiveAsync>d__17.MoveNext` | 10.5 % |
| `Infrastructure.Lebenszyklus.RahmenActor.ReceiveAsync` | 10.5 % |
| `Domain.Pipeline.Bildaufbereitung.OpenCvBildaufbereitung+<Auf>d__5.MoveNext` | 10.4 % |
| `….__Canon,Marten.Internal.Sessions.QuerySession+<LoadOneAsync>d__78`1[System.__Canon]].ExecutionContextCallback` | 10.4 % |
| `…f`2[System.__Canon,System.__Canon],Domain.Pipeline.Bildaufbereitung.OpenCvBildaufbereitung+<Auf>d__5].MoveNext` | 10.3 % |
| `…on,System.__Canon],Domain.Pipeline.Bildaufbereitung.OpenCvBildaufbereitung+<Auf>d__5].ExecutionContextCallback` | 10.3 % |
| `Marten.Internal.Sessions.DocumentSessionBase+<ExecuteBatchAsync>d__74.MoveNext` | 10.3 % |
| `…em.__Canon,Marten.Internal.Sessions.QuerySession+<LoadAsync>d__106`1[System.__Canon]].ExecutionContextCallback` | 9.0 % |
| `…hineBox`1[System.__Canon,Infrastructure.Projections.Generated.GeneratedQueryDienst+<FrageAsync>d__33].MoveNext` | 8.8 % |
