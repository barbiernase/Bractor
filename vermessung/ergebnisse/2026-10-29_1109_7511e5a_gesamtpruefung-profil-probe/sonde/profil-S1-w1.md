# CPU-Profil des Hosts

Stichproben 19316 (≈ 10 ms je Thread), davon arbeitend (innerster Rahmen keine Warte-Methode) 10472 ≈ 104.7 Thread-Sekunden; Markierung: UNMANAGED_CODE_TIME 19291, CPU_TIME 25

Die Anteile beziehen sich auf die arbeitenden Stichproben.

## Arbeit nach Bibliothek (innerster Rahmen außerhalb der Laufzeit)

| Bibliothek | Anteil |
|---|---:|
| OpenCvSharp | 30.9 % |
| Npgsql | 28.1 % |
| Laufzeit allein | 9.4 % |
| Infrastructure | 9.1 % |
| Marten | 6.7 % |
| JasperFx.RuntimeCompiler | 2.3 % |
| Abstractions | 2.1 % |
| Polly.Core | 2.0 % |
| Domain.Laufzeit | 1.9 % |
| Grpc.AspNetCore.Server | 1.8 % |
| Proto.Actor | 1.3 % |
| JasperFx | 0.8 % |
| JasperFx.Events | 0.7 % |
| Google.Protobuf | 0.6 % |
| Domain.Infrastructure | 0.5 % |
| Domain.Leitung | 0.4 % |
| Proto.Cluster | 0.4 % |
| Domain | 0.3 % |
| Domain.Pipeline | 0.2 % |
| Core | 0.1 % |

## Methoden selbst (exklusiv)

| Methode | Anteil |
|---|---:|
| `Interop+Sys.Send` | 15.2 % |
| `OpenCvSharp.Internal.NativeMethods.imgcodecs_imwrite` | 11.3 % |
| `OpenCvSharp.Cv2.Resize` | 9.7 % |
| `?` | 3.8 % |
| `OpenCvSharp.Cv2.CvtColor` | 3.3 % |
| `OpenCvSharp.Cv2.EqualizeHist` | 2.7 % |
| `Npgsql.Internal.NpgsqlConnector.ResetCancellation` | 2.5 % |
| `System.Diagnostics.Metrics.CounterAggregator.Update` | 2.4 % |
| `System.Guid.NewGuid` | 2.3 % |
| `dynamicClass.ResolveService` | 2.0 % |
| `Interop+Sys.Receive` | 1.8 % |
| `System.Diagnostics.Debugger.NotifyOfCrossThreadDependency` | 1.6 % |
| `Interop+Kernel32.ReleaseSemaphore` | 1.3 % |
| `OpenCvSharp.Cv2.Merge` | 1.3 % |
| `OpenCvSharp.Internal.NativeMethods.imgcodecs_imread` | 1.2 % |
| `OpenCvSharp.Cv2.Split` | 1.2 % |
| `System.Reflection.RuntimeAssembly.InternalLoad` | 1.1 % |
| `System.DateTime.get_UtcNow` | 1.1 % |
| `System.Text.Json.ReadStack.Push` | 1.0 % |
| `Marten.Storage.MartenDatabase.CreateConnection` | 0.9 % |
| `System.Text.Json.WriteStack.Push` | 0.9 % |
| `System.Security.Cryptography.HashProviderDispenser+OneShotHashProvider.HashData` | 0.9 % |
| `Npgsql.NpgsqlCommand..ctor` | 0.9 % |
| `System.Collections.Generic.List`1[System.__Canon].set_Capacity` | 0.8 % |
| `System.Net.Sockets.SocketAsyncContext+OperationQueue`1[System.__Canon].ProcessSyncEventOrGetAsyncEvent` | 0.8 % |
| `Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol.get_RequestAborted` | 0.8 % |
| `System.Buffer.Memmove` | 0.8 % |
| `System.Text.Json.ReadStack.Pop` | 0.8 % |
| `System.Collections.Generic.List`1[System.__Canon].AddRange` | 0.8 % |
| `Npgsql.Util.ResettableCancellationTokenSource.Start` | 0.7 % |

## Methoden gesamt (inklusiv, ohne Laufzeit-Rahmen)

| Methode | Anteil |
|---|---:|
| `Npgsql.NpgsqlCommand+<ExecuteReader>d__120.MoveNext` | 37.7 % |
| `…ystem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 27.7 % |
| `…xecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 27.1 % |
| `Polly.ResiliencePipeline+<ExecuteAsync>d__9`2[System.__Canon,System.__Canon].MoveNext` | 22.3 % |
| `Polly.ResiliencePipeline+<>c__9`2+<<ExecuteAsync>b__9_0>d[System.__Canon,System.__Canon].MoveNext` | 21.4 % |
| `Npgsql.Internal.NpgsqlConnector+<ReadMessageLong>d__235.MoveNext` | 21.3 % |
| `Npgsql.Internal.NpgsqlReadBuffer+<<Ensure>g__EnsureLong|55_0>d.MoveNext` | 21.1 % |
| `Npgsql.NpgsqlDataReader+<NextResult>d__52.MoveNext` | 21.1 % |
| `Npgsql.NpgsqlCommand.ExecuteReaderAsync` | 20.7 % |
| `…TaskMethodBuilder`1+AsyncStateMachineBox`1[System.__Canon,Npgsql.NpgsqlCommand+<ExecuteReader>d__120].MoveNext` | 20.0 % |
| `…ueTaskAsync|4_0>d`2[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 19.5 % |
| `…cTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Boolean,Npgsql.NpgsqlDataReader+<NextResult>d__52].MoveNext` | 19.2 % |
| `…eTaskAsync|4_0>d`2[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 19.1 % |
| `Domain.Pipeline.Bildaufbereitung.OpenCvBildaufbereitung+<Auf>d__5.MoveNext` | 18.9 % |
| `…f`2[System.__Canon,System.__Canon],Domain.Pipeline.Bildaufbereitung.OpenCvBildaufbereitung+<Auf>d__5].MoveNext` | 18.9 % |
| `…on,System.__Canon],Domain.Pipeline.Bildaufbereitung.OpenCvBildaufbereitung+<Auf>d__5].ExecutionContextCallback` | 18.9 % |
| `…stem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]]].MoveNext` | 18.7 % |
| `…ecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 18.1 % |
| `Marten.Internal.Sessions.QuerySession+<LoadOneAsync>d__78`1[System.__Canon].MoveNext` | 17.8 % |
| `Marten.Internal.Sessions.QuerySession+<LoadAsync>d__106`1[System.__Canon].MoveNext` | 17.5 % |
| `…ineBox`1[System.__Canon,Polly.ResiliencePipeline+<ExecuteAsync>d__9`2[System.__Canon,System.__Canon]].MoveNext` | 17.4 % |
| `Marten.Internal.Sessions.AutoClosingLifetime+<ExecuteReaderAsync>d__12.MoveNext` | 16.1 % |
| `Npgsql.NpgsqlCommand+<<Write>g__WriteExecute|101_0>d.MoveNext` | 16.0 % |
| `Npgsql.Internal.NpgsqlWriteBuffer+<Flush>d__38.MoveNext` | 15.2 % |
| `Interop+Sys.Send` | 15.2 % |
| `Infrastructure.Projections.Generated.GeneratedQueryDienst+<FrageAsync>d__33.MoveNext` | 14.6 % |
| `….__Canon,Marten.Internal.Sessions.QuerySession+<LoadOneAsync>d__78`1[System.__Canon]].ExecutionContextCallback` | 13.2 % |
| `…hineBox`1[System.__Canon,Marten.Internal.Sessions.QuerySession+<LoadOneAsync>d__78`1[System.__Canon]].MoveNext` | 13.2 % |
| `…em.__Canon],Polly.ResiliencePipeline+<>c__9`2+<<ExecuteAsync>b__9_0>d[System.__Canon,System.__Canon]].MoveNext` | 12.7 % |
| `…em.__Canon,Marten.Internal.Sessions.QuerySession+<LoadAsync>d__106`1[System.__Canon]].ExecutionContextCallback` | 11.6 % |
| `…achineBox`1[System.__Canon,Marten.Internal.Sessions.QuerySession+<LoadAsync>d__106`1[System.__Canon]].MoveNext` | 11.6 % |
| `Polly.Utils.Pipeline.CompositeComponent.ExecuteCoreWithoutTelemetry` | 11.3 % |
| `Polly.Utils.Pipeline.BridgeComponent`1[System.__Canon].ExecuteCore` | 11.3 % |
| `OpenCvSharp.Cv2.ImWrite` | 11.3 % |
| `Domain.Pipeline.Bildaufbereitung.OpenCvBildaufbereitung+<>c__DisplayClass5_0.<Auf>b__0` | 11.3 % |
| `OpenCvSharp.Internal.NativeMethods.imgcodecs_imwrite` | 11.3 % |
| `Polly.Retry.RetryResilienceStrategy`1[System.__Canon].ExecuteCore` | 11.2 % |
| `Proto.Mailbox.DefaultMailbox.Execute` | 11.1 % |
| `Proto.Mailbox.DefaultMailbox.RunAsync` | 11.0 % |
| `Proto.Mailbox.DefaultMailbox.ProcessMessages` | 11.0 % |
| `…ystem.__Canon,Marten.Internal.Sessions.AutoClosingLifetime+<ExecuteReaderAsync>d__12].ExecutionContextCallback` | 10.9 % |
| `…nfrastructure.Projections.Generated.GeneratedQueryDienst+<>c__DisplayClass32_0+<<ExecuteAsync>b__12>d.MoveNext` | 10.7 % |
| `Marten.Internal.Sessions.DocumentSessionBase+<SaveChangesAsync>d__66.MoveNext` | 10.4 % |
| `Infrastructure.Projections.ProjektionsStaende+<StaendeAsync>d__10.MoveNext` | 10.4 % |
| `Domain.Projections.ImagePair.ImagePairReader+<Frage>d__0.MoveNext` | 10.1 % |
