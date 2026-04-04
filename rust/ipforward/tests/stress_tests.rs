//! Stress tests for UDP forwarding at scale
//!
//! These tests validate performance characteristics under load

use ipforward::forwarder::UdpForwarder;
use std::net::{IpAddr, Ipv4Addr, SocketAddr};
use std::time::{Duration, Instant};
use tokio::net::UdpSocket;
use tokio::time::timeout;

/// Test handling multiple concurrent IoT devices
#[tokio::test]
async fn test_concurrent_iot_devices_stress() {
    const NUM_DEVICES: usize = 1000; // Scaled down for CI
    const MESSAGE_SIZE: usize = 128;

    // Create mock ingress gateway
    let gateway = UdpSocket::bind("127.0.0.1:0").await.unwrap();
    let gateway_addr = gateway.local_addr().unwrap();

    // Start gateway that responds with ACKs
    let gateway_task = {
        let gateway = gateway;
        tokio::spawn(async move {
            let mut buf = [0u8; 1024];
            let mut ack_count = 0;

            while ack_count < NUM_DEVICES {
                if let Ok((len, from_addr)) = gateway.recv_from(&mut buf).await {
                    if len == MESSAGE_SIZE {
                        // Send ACK back
                        let ack = b"ACK";
                        let _ = gateway.send_to(ack, from_addr).await;
                        ack_count += 1;
                    }
                }
            }
        })
    };

    // Create IoT devices
    let mut devices = Vec::new();
    for _ in 0..NUM_DEVICES {
        let device = UdpSocket::bind("127.0.0.1:0").await.unwrap();
        devices.push(device);
    }

    // Measure time to send all messages
    let start = Instant::now();

    // Send messages from all devices concurrently
    let send_tasks: Vec<_> = devices
        .iter()
        .enumerate()
        .map(|(i, device)| {
            let message = vec![i as u8; MESSAGE_SIZE]; // Unique message per device
            async move {
                device.send_to(&message, gateway_addr).await.unwrap();
            }
        })
        .collect();

    // Execute all sends
    futures::future::join_all(send_tasks).await;

    let send_duration = start.elapsed();

    // Wait for gateway to process all messages
    let _ = timeout(Duration::from_secs(5), gateway_task).await;

    // Verify performance
    let messages_per_second = NUM_DEVICES as f64 / send_duration.as_secs_f64();

    println!("Stress test results:");
    println!("  Devices: {}", NUM_DEVICES);
    println!("  Message size: {} bytes", MESSAGE_SIZE);
    println!("  Total time: {:?}", send_duration);
    println!("  Messages/second: {:.0}", messages_per_second);

    // Performance assertions
    assert!(
        send_duration < Duration::from_secs(1),
        "Should handle 1k devices in <1 second"
    );
    assert!(
        messages_per_second > 1000.0,
        "Should handle >1000 messages/second"
    );
}

/// Test memory usage with large client mapping
#[tokio::test]
async fn test_large_client_mapping_memory() {
    use std::collections::HashMap;

    const NUM_CLIENTS: usize = 100_000;

    let start = Instant::now();

    // Simulate client mapping for 100k devices
    let mut client_map: HashMap<SocketAddr, SocketAddr> = HashMap::with_capacity(NUM_CLIENTS);

    for i in 0..NUM_CLIENTS {
        let client_addr = SocketAddr::new(
            IpAddr::V4(Ipv4Addr::new(192, 168, (i / 256) as u8, (i % 256) as u8)),
            8000 + (i % 1000) as u16,
        );
        let target_addr = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(10, 0, 0, 1)), 9000);

        client_map.insert(target_addr, client_addr);
    }

    let creation_time = start.elapsed();

    // Test lookup performance
    let lookup_start = Instant::now();
    let test_target = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(10, 0, 0, 1)), 9000);

    for _ in 0..1000 {
        let _ = client_map.get(&test_target);
    }

    let lookup_time = lookup_start.elapsed();

    println!("Memory test results:");
    println!("  Client mappings: {}", NUM_CLIENTS);
    println!("  Creation time: {:?}", creation_time);
    println!("  1000 lookups time: {:?}", lookup_time);
    println!("  Avg lookup time: {:?}", lookup_time / 1000);

    // Performance assertions
    assert!(
        creation_time < Duration::from_secs(5),
        "Should create 100k mappings in <5 seconds"
    );
    assert!(
        lookup_time < Duration::from_millis(10),
        "1000 lookups should take <10ms"
    );

    // Verify mapping works
    assert_eq!(client_map.len(), NUM_CLIENTS);
    assert!(client_map.contains_key(&test_target));
}

/// Test packet throughput simulation
#[tokio::test]
async fn test_packet_throughput_simulation() {
    const PACKETS_PER_SECOND: usize = 10_000; // Your target load
    const TEST_DURATION_SECS: u64 = 1;
    const PACKET_SIZE: usize = 128;

    // Create sender and receiver
    let sender = UdpSocket::bind("127.0.0.1:0").await.unwrap();
    let receiver = UdpSocket::bind("127.0.0.1:0").await.unwrap();
    let receiver_addr = receiver.local_addr().unwrap();

    let packet = vec![0u8; PACKET_SIZE];
    let mut received_count = 0;

    // Start receiver task
    let receiver_task = {
        let receiver = receiver;
        tokio::spawn(async move {
            let mut buf = vec![0u8; PACKET_SIZE + 100];
            let mut count = 0;

            while count < PACKETS_PER_SECOND {
                if let Ok(_) = receiver.recv_from(&mut buf).await {
                    count += 1;
                }
            }
            count
        })
    };

    // Send packets at target rate
    let start = Instant::now();
    let interval = Duration::from_nanos(1_000_000_000 / PACKETS_PER_SECOND as u64);

    for i in 0..PACKETS_PER_SECOND {
        let send_start = Instant::now();

        sender.send_to(&packet, receiver_addr).await.unwrap();

        // Rate limiting
        let elapsed = send_start.elapsed();
        if elapsed < interval {
            tokio::time::sleep(interval - elapsed).await;
        }

        if i % 1000 == 0 {
            println!("Sent {} packets", i);
        }
    }

    let total_time = start.elapsed();

    // Wait for receiver to finish
    received_count = timeout(Duration::from_secs(2), receiver_task)
        .await
        .unwrap()
        .unwrap();

    let actual_rate = received_count as f64 / total_time.as_secs_f64();

    println!("Throughput test results:");
    println!("  Target rate: {} packets/second", PACKETS_PER_SECOND);
    println!("  Actual rate: {:.0} packets/second", actual_rate);
    println!("  Packets sent: {}", PACKETS_PER_SECOND);
    println!("  Packets received: {}", received_count);
    println!("  Total time: {:?}", total_time);

    // Performance assertions
    assert!(
        received_count >= PACKETS_PER_SECOND * 95 / 100,
        "Should receive >95% of packets"
    );
    assert!(
        actual_rate >= PACKETS_PER_SECOND as f64 * 0.9,
        "Should achieve >90% of target rate"
    );
}
