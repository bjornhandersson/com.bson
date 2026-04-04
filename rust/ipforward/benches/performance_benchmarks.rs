//! Performance benchmarks for UDP forwarding at IoT scale
//!
//! Tests performance characteristics for 100,000 devices sending 128 bytes every 10 seconds

use criterion::{black_box, criterion_group, criterion_main, BenchmarkId, Criterion, Throughput};
use std::net::{IpAddr, Ipv4Addr, SocketAddr};
use std::time::Duration;
use tokio::net::UdpSocket;
use tokio::runtime::Runtime;

/// Benchmark UDP packet forwarding throughput
fn bench_udp_forwarding_throughput(c: &mut Criterion) {
    let rt = Runtime::new().unwrap();

    let mut group = c.benchmark_group("udp_forwarding_throughput");

    // Test different packet sizes
    for packet_size in [64, 128, 256, 512, 1024].iter() {
        group.throughput(Throughput::Bytes(*packet_size as u64));

        group.bench_with_input(
            BenchmarkId::new("packet_forward", packet_size),
            packet_size,
            |b, &size| {
                b.to_async(&rt).iter(|| async {
                    // Create test sockets
                    let sender = UdpSocket::bind("127.0.0.1:0").await.unwrap();
                    let receiver = UdpSocket::bind("127.0.0.1:0").await.unwrap();
                    let receiver_addr = receiver.local_addr().unwrap();

                    // Create test packet
                    let packet = vec![0u8; size];

                    // Measure forwarding time
                    black_box(sender.send_to(&packet, receiver_addr).await.unwrap());

                    let mut buf = vec![0u8; size + 100];
                    black_box(receiver.recv_from(&mut buf).await.unwrap());
                });
            },
        );
    }
    group.finish();
}

/// Benchmark concurrent connections handling
fn bench_concurrent_connections(c: &mut Criterion) {
    let rt = Runtime::new().unwrap();

    let mut group = c.benchmark_group("concurrent_connections");

    // Test different numbers of concurrent connections
    for num_connections in [10, 100, 1000, 5000].iter() {
        group.bench_with_input(
            BenchmarkId::new("concurrent_sockets", num_connections),
            num_connections,
            |b, &num_conn| {
                b.to_async(&rt).iter(|| async {
                    let mut sockets = Vec::new();

                    // Create multiple concurrent sockets
                    for _ in 0..num_conn {
                        let socket = UdpSocket::bind("127.0.0.1:0").await.unwrap();
                        sockets.push(socket);
                    }

                    black_box(sockets.len());
                });
            },
        );
    }
    group.finish();
}

/// Benchmark IoT-scale message processing
fn bench_iot_scale_simulation(c: &mut Criterion) {
    let rt = Runtime::new().unwrap();

    let mut group = c.benchmark_group("iot_scale_simulation");
    group.sample_size(10); // Fewer samples for heavy tests
    group.measurement_time(Duration::from_secs(30));

    // Simulate IoT device message patterns
    for num_devices in [100, 1000, 10000].iter() {
        group.throughput(Throughput::Elements(*num_devices as u64));

        group.bench_with_input(
            BenchmarkId::new("iot_devices", num_devices),
            num_devices,
            |b, &num_dev| {
                b.to_async(&rt).iter(|| async {
                    // Create gateway socket
                    let gateway = UdpSocket::bind("127.0.0.1:0").await.unwrap();
                    let gateway_addr = gateway.local_addr().unwrap();

                    // Create IoT device sockets
                    let mut devices = Vec::new();
                    for _ in 0..num_dev {
                        let device = UdpSocket::bind("127.0.0.1:0").await.unwrap();
                        devices.push(device);
                    }

                    // Simulate IoT message burst (128 bytes each)
                    let iot_message = vec![0u8; 128];

                    // Send messages from all devices concurrently
                    let send_tasks: Vec<_> = devices
                        .iter()
                        .map(|device| {
                            let msg = iot_message.clone();
                            async move {
                                device.send_to(&msg, gateway_addr).await.unwrap();
                            }
                        })
                        .collect();

                    // Execute all sends concurrently
                    futures::future::join_all(send_tasks).await;

                    black_box(num_dev);
                });
            },
        );
    }
    group.finish();
}

/// Benchmark memory usage patterns
fn bench_memory_efficiency(c: &mut Criterion) {
    let rt = Runtime::new().unwrap();

    let mut group = c.benchmark_group("memory_efficiency");

    // Test client mapping memory usage
    for num_clients in [1000, 10000, 100000].iter() {
        group.bench_with_input(
            BenchmarkId::new("client_mapping", num_clients),
            num_clients,
            |b, &num_clients| {
                b.iter(|| {
                    use std::collections::HashMap;
                    use std::net::SocketAddr;

                    // Simulate client mapping storage
                    let mut client_map: HashMap<SocketAddr, SocketAddr> = HashMap::new();

                    for i in 0..num_clients {
                        let client_addr = SocketAddr::new(
                            IpAddr::V4(Ipv4Addr::new(192, 168, (i / 256) as u8, (i % 256) as u8)),
                            8000 + (i % 1000) as u16,
                        );
                        let target_addr =
                            SocketAddr::new(IpAddr::V4(Ipv4Addr::new(10, 0, 0, 1)), 9000);

                        client_map.insert(target_addr, client_addr);
                    }

                    black_box(client_map.len());
                });
            },
        );
    }
    group.finish();
}

/// Benchmark packet processing latency
fn bench_packet_latency(c: &mut Criterion) {
    let rt = Runtime::new().unwrap();

    let mut group = c.benchmark_group("packet_latency");
    group.sample_size(1000);

    group.bench_function("single_packet_roundtrip", |b| {
        b.to_async(&rt).iter(|| async {
            // Create sender and receiver
            let sender = UdpSocket::bind("127.0.0.1:0").await.unwrap();
            let receiver = UdpSocket::bind("127.0.0.1:0").await.unwrap();
            let receiver_addr = receiver.local_addr().unwrap();

            // IoT-typical 128-byte message
            let message = vec![0u8; 128];

            // Measure round-trip time
            let start = std::time::Instant::now();

            sender.send_to(&message, receiver_addr).await.unwrap();

            let mut buf = vec![0u8; 256];
            receiver.recv_from(&mut buf).await.unwrap();

            let elapsed = start.elapsed();
            black_box(elapsed);
        });
    });

    group.finish();
}

criterion_group!(
    benches,
    bench_udp_forwarding_throughput,
    bench_concurrent_connections,
    bench_iot_scale_simulation,
    bench_memory_efficiency,
    bench_packet_latency
);

criterion_main!(benches);
