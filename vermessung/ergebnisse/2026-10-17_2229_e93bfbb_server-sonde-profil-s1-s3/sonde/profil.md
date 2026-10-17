# CPU-Profil des Hosts

Stichproben 59595 (≈ 10 ms je Thread), davon arbeitend (innerster Rahmen keine Warte-Methode) 28564 ≈ 285.6 Thread-Sekunden; Markierung: UNMANAGED_CODE_TIME 59561, CPU_TIME 34

Die Anteile beziehen sich auf die arbeitenden Stichproben.

## Arbeit nach Bibliothek (innerster Rahmen außerhalb der Laufzeit)

| Bibliothek | Anteil |
|---|---:|
| Npgsql | 22.4 % |
| OpenCvSharp | 19.0 % |
| Infrastructure | 13.7 % |
| Marten | 12.6 % |
| Laufzeit allein | 10.9 % |
| Abstractions | 7.0 % |
| Proto.Actor | 2.6 % |
| Polly.Core | 2.2 % |
| Pipelines.Sockets.Unofficial | 2.0 % |
| StackExchange.Redis | 1.6 % |
| FastExpressionCompiler | 1.1 % |
| JasperFx.Events | 1.0 % |
| Proto.Cluster | 0.9 % |
| Weasel.Postgresql | 0.9 % |
| Anonymously Hosted DynamicMethods Assembly | 0.6 % |
| JasperFx.RuntimeCompiler | 0.4 % |
| JasperFx | 0.3 % |
| Domain.Projections | 0.2 % |
| Domain.Pipeline | 0.2 % |
| Domain | 0.1 % |

## Methoden selbst (exklusiv)

| Methode | Anteil |
|---|---:|
| `Interop+Sys.Send` | 12.4 % |
| `OpenCvSharp.Internal.NativeMethods.imgcodecs_imwrite` | 8.3 % |
| `Marten.Linq.Parsing.LinqInternalExtensions.ReduceToConstant` | 5.6 % |
| `OpenCvSharp.Internal.NativeMethods.imgcodecs_imread` | 4.6 % |
| `System.Guid.NewGuid` | 4.1 % |
| `?` | 3.5 % |
| `OpenCvSharp.Cv2.Resize` | 3.1 % |
| `System.Diagnostics.Metrics.CounterAggregator.Update` | 2.8 % |
| `Interop+Sys.Receive` | 2.4 % |
| `System.Collections.Generic.Dictionary`2[System.__Canon,System.__Canon].Resize` | 2.1 % |
| `System.Collections.Generic.SegmentedArrayBuilder`1[System.__Canon].ToList` | 2.0 % |
| `System.Collections.Generic.List`1[System.__Canon].set_Capacity` | 1.9 % |
| `Npgsql.Util.ResettableCancellationTokenSource.Start` | 1.9 % |
| `Interop+Kernel32.ReleaseSemaphore` | 1.8 % |
| `System.Security.Cryptography.HashProviderDispenser+OneShotHashProvider.HashData` | 1.7 % |
| `Npgsql.Internal.NpgsqlConnector.ResetCancellation` | 1.7 % |
| `System.Text.Json.ReadStack.Push` | 1.3 % |
| `System.Diagnostics.Debugger.NotifyOfCrossThreadDependency` | 1.3 % |
| `Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol.get_RequestAborted` | 1.2 % |
| `System.Text.Json.WriteStack.Push` | 1.2 % |
| `System.DateTime.get_UtcNow` | 1.1 % |
| `OpenCvSharp.Cv2.CvtColor` | 1.0 % |
| `Microsoft.AspNetCore.Routing.Matching.AcceptsMatcherPolicy+ConsumesPolicyJumpTable.GetDestination` | 1.0 % |
| `Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.TimeoutControl.StartRequestBody` | 0.9 % |
| `OpenCvSharp.Cv2.EqualizeHist` | 0.8 % |
| `Npgsql.NpgsqlCommand..ctor` | 0.8 % |
| `System.Text.Json.WriteStack.Pop` | 0.8 % |
| `System.Threading.Channels.UnboundedChannel`1+UnboundedChannelWriter[System.__Canon].TryWrite` | 0.8 % |
| `System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.__Canon].GetStateMachineBox` | 0.8 % |
| `System.IO.Pipelines.Pipe.AdvanceReader` | 0.8 % |

## Methoden gesamt (inklusiv, ohne Laufzeit-Rahmen)

| Methode | Anteil |
|---|---:|
| `Npgsql.NpgsqlCommand+<ExecuteReader>d__120.MoveNext` | 38.9 % |
| `Npgsql.Internal.NpgsqlReadBuffer+<<Ensure>g__EnsureLong|55_0>d.MoveNext` | 28.3 % |
| `Npgsql.Internal.NpgsqlConnector+<ReadMessageLong>d__235.MoveNext` | 28.0 % |
| `Npgsql.NpgsqlDataReader+<NextResult>d__52.MoveNext` | 26.5 % |
| `…TaskMethodBuilder`1+AsyncStateMachineBox`1[System.__Canon,Npgsql.NpgsqlCommand+<ExecuteReader>d__120].MoveNext` | 25.9 % |
| `…cTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Boolean,Npgsql.NpgsqlDataReader+<NextResult>d__52].MoveNext` | 25.3 % |
| `…ystem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 23.1 % |
| `…xecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 21.7 % |
| `Polly.ResiliencePipeline+<ExecuteAsync>d__9`2[System.__Canon,System.__Canon].MoveNext` | 16.8 % |
| `Polly.ResiliencePipeline+<>c__9`2+<<ExecuteAsync>b__9_0>d[System.__Canon,System.__Canon].MoveNext` | 15.8 % |
| `…t.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol+<ProcessRequests>d__238`1[System.__Canon].MoveNext` | 14.1 % |
| `….AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol+<ProcessRequests>d__238`1[System.__Canon]].MoveNext` | 14.1 % |
| `…stem.__Canon,System.ValueTuple`2[System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]]].MoveNext` | 12.9 % |
| `…ueTaskAsync|4_0>d`2[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]].MoveNext` | 12.6 % |
| `…eTaskAsync|4_0>d`2[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 12.6 % |
| `Npgsql.Internal.NpgsqlWriteBuffer+<Flush>d__38.MoveNext` | 12.5 % |
| `Polly.Utils.Pipeline.BridgeComponent`1[System.__Canon].ExecuteCore` | 12.5 % |
| `Polly.Utils.Pipeline.CompositeComponent.ExecuteCoreWithoutTelemetry` | 12.5 % |
| `Polly.Retry.RetryResilienceStrategy`1[System.__Canon].ExecuteCore` | 12.4 % |
| `…er.Kestrel.Core.Internal.Http.HttpProtocol+<ProcessRequests>d__238`1[System.__Canon]].ExecutionContextCallback` | 12.4 % |
| `Interop+Sys.Send` | 12.4 % |
| `Marten.Internal.Sessions.QuerySession+<LoadOneAsync>d__78`1[System.__Canon].MoveNext` | 12.2 % |
| `Domain.Projections.ProjectionQueryService+<ExecuteAsync>d__9.MoveNext` | 11.9 % |
| `…ecuteCore>b__4_1>d[System.__Canon,System.__Canon,System.ValueTuple`2[System.__Canon,System.__Canon]]].MoveNext` | 11.9 % |
| `Marten.Internal.Sessions.QuerySession+<LoadAsync>d__106`1[System.__Canon].MoveNext` | 11.9 % |
| `…uestDelegateFactory+<>c__DisplayClass101_2+<<HandleRequestBodyAndCompileRequestDelegateForJson>b__2>d.MoveNext` | 11.8 % |
| `…erver.Kestrel.Core.Internal.Http.HttpProtocol+<ProcessRequests>d__238`1[System.__Canon]].ExecuteFromThreadPool` | 11.8 % |
| `Npgsql.NpgsqlCommand+<<Write>g__WriteExecute|101_0>d.MoveNext` | 11.7 % |
| `Infrastructure.Persistence.MartenEventStore+<ReadPartitionAsync>d__24.MoveNext` | 11.5 % |
| `Infrastructure.Eingang.JsonEingang+<>c+<<MapEingang>b__2_2>d.MoveNext` | 11.5 % |
| `Proto.Mailbox.DefaultMailbox.RunAsync` | 11.3 % |
| `Proto.Mailbox.DefaultMailbox.Execute` | 11.3 % |
| `Proto.Mailbox.DefaultMailbox.ProcessMessages` | 11.3 % |
| `Marten.Internal.Sessions.AutoClosingLifetime+<ExecuteReaderAsync>d__12.MoveNext` | 11.2 % |
| `Infrastructure.Persistence.MartenEventStore+<LeseStreamAsync>d__13.MoveNext` | 10.6 % |
| `…ateMachineBox`1[System.__Canon,Infrastructure.Persistence.MartenEventStore+<ReadPartitionAsync>d__24].MoveNext` | 10.1 % |
| `Infrastructure.Persistence.MartenEventStore+<GesetztAsync>d__25.MoveNext` | 9.9 % |
| `…ore.Http.RequestDelegateFactory+<>c__DisplayClass101_2.<HandleRequestBodyAndCompileRequestDelegateForJson>b__2` | 9.9 % |
| `Marten.Linq.MartenLinqQueryable`1+<ToListAsync>d__36`1[System.__Canon,System.__Canon].MoveNext` | 9.8 % |
| `…ineBox`1[System.__Canon,Polly.ResiliencePipeline+<ExecuteAsync>d__9`2[System.__Canon,System.__Canon]].MoveNext` | 9.8 % |
| `…System.__Canon,Infrastructure.Persistence.MartenEventStore+<ReadPartitionAsync>d__24].ExecutionContextCallback` | 9.2 % |
| `Marten.Internal.Sessions.DocumentSessionBase+<SaveChangesAsync>d__66.MoveNext` | 9.1 % |
| `…Tuple`2[System.Int64,System.Boolean],Infrastructure.Persistence.MartenEventStore+<GesetztAsync>d__25].MoveNext` | 9.0 % |
| `dynamicClass.lambda_method71` | 8.9 % |
| `Polly.ResiliencePipeline.ExecuteAsync` | 8.9 % |
