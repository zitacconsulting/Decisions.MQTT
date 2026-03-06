# Decisions MQTT Module

> ⚠️ **Important:** Use this module at your own risk. See the **Disclaimer** section below.

## Overview

**Decisions MQTT Module** is an integration module for the Decisions no-code automation platform that enables real-time message processing via MQTT brokers. It provides queue management capabilities with support for MQTT 3.1.1 and MQTT 5.0, wildcard topic subscriptions, shared subscriptions, persistent sessions, Last Will and Testament (LWT), cluster-safe lease management, and flow steps for publishing messages.

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Configuration Options](#configuration-options)
- [Flow Steps](#flow-steps)
- [MQTT 5.0 Features](#mqtt-50-features)
- [Cluster Deployments](#cluster-deployments)
- [Monitoring](#monitoring)
- [Building from Source](#building-from-source)
- [Disclaimer](#disclaimer)

## Features

### Broker Connectivity
- MQTT 3.1.1 and MQTT 5.0 protocol support
- TCP and WebSocket transport (including WSS/TLS)
- TLS/SSL encryption with optional untrusted certificate bypass
- Username/password authentication
- Configurable Keep Alive and Connection Timeout
- Custom Client ID override (independent of connection override settings)
- Automatic thread restart on broker connection loss

### Topic Subscriptions
- Exact topic subscriptions (e.g. `sensors/temperature`)
- Single-level wildcard `+` (e.g. `sensors/+/temperature`)
- Multi-level wildcard `#` (e.g. `factory/line1/#`)
- Topic filter syntax validated at configuration time
- Shared subscriptions for consumer groups (MQTT 5.0)

### Message Processing
- Configurable QoS levels (0 = At Most Once, 1 = At Least Once, 2 = Exactly Once)
- Persistent sessions for guaranteed delivery after reconnect
- Configurable in-memory message buffer with buffer-full warnings in the log
- MQTT 5.0 User Properties passed as flow message headers

### Reliability
- Cluster-safe single-connection lease management
- Automatic failover between cluster nodes
- Last Will and Testament (LWT) for offline detection
- Connection loss detection triggers automatic Decisions thread restart

### Flow Integration
- **Publish MQTT Message** — publish to any topic from a flow
- **Enable MQTT Queue** — enable a queue from a flow
- **Disable MQTT Queue** — disable a queue from a flow

## Requirements

- **Decisions Platform**: Version 9.21.0 or higher
- **MQTT Broker**: Any standards-compliant MQTT 3.1.1 or 5.0 broker (e.g. Mosquitto, EMQX, HiveMQ, AWS IoT Core)

## Installation

### Option 1: Install Pre-built Module
1. Download the compiled module (`.zip` file)
2. Log into Decisions Portal
3. Navigate to **System > Administration > Features**
4. Click **Install Module**
5. Upload the module file
6. Restart the Decisions service if prompted

### Option 2: Build and Install
See the [Building from Source](#building-from-source) section below.

## Quick Start

1. **Install the Module**
   - Upload via Decisions Portal (System > Administration > Features)

2. **Configure Global Settings**
   - Navigate to **System > Settings > Message Queue Settings > MQTT Settings**
   - Set broker host, port, credentials, and protocol version

3. **Create an MQTT Queue**
   - Go to **Manage > Jobs & Events > Messaging > Queues**
   - Click **Add MQTT Queue**
   - Set the **Topic Filter** (e.g. `sensors/#`)
   - Choose the **Quality of Service** level

4. **Create a Queue Handler**
   - Create a flow to process incoming messages
   - Bind the flow as a Queue Handler to your MQTT queue

5. **Start Processing**
   - Enable the queue to begin receiving messages from the broker

## Configuration Options

### Global Settings
Configure defaults under **System > Settings > Message Queue Settings > MQTT Settings**.
These apply to all queues unless overridden per queue.

| Setting | Description | Default |
|---------|-------------|---------|
| Broker Host | MQTT broker hostname or IP | — |
| Use Default Port | Use standard port based on TLS/WebSocket settings | true |
| Port | Custom port number | 1883 |
| Use TLS/SSL | Enable TLS encryption | false |
| Allow Untrusted Certificates | Skip TLS certificate validation (not recommended for production) | false |
| Use WebSocket Transport | Connect via WebSocket instead of TCP | false |
| WebSocket Path | WebSocket endpoint path | /mqtt |
| Username | Broker username | — |
| Password | Broker password | — |
| Protocol Version | MQTT 3.1.1 or 5.0 | 3.1.1 |
| Keep Alive (seconds) | Heartbeat interval | 60 |
| Connection Timeout (seconds) | Max time to establish connection | 10 |
| Persistent Session | Resume subscription state after reconnect | false |

**Default ports by transport:**
| Transport | Plain | TLS |
|-----------|-------|-----|
| TCP | 1883 | 8883 |
| WebSocket | 8083 | 8084 |

### Per-Queue Settings

#### 1 Definition
| Setting | Description |
|---------|-------------|
| Topic Filter | MQTT topic to subscribe to. Supports `+` (single level) and `#` (multi-level, must be last) wildcards |
| Quality of Service (QoS) | 0 = At Most Once, 1 = At Least Once, 2 = Exactly Once |

#### 2 Connection
Enable **Override Global Settings** to configure broker settings per queue, overriding the global defaults.

| Setting | Description |
|---------|-------------|
| Broker Host | MQTT broker hostname or IP |
| Use Default Port | Use standard port based on TLS/WebSocket settings |
| Port | Custom port number |
| Use TLS/SSL | Enable TLS encryption |
| Allow Untrusted Certificates | Skip TLS certificate validation |
| Use WebSocket Transport | Connect via WebSocket instead of TCP |
| WebSocket Path | WebSocket endpoint path |
| Username | Broker username |
| Password | Broker password |
| Protocol Version | MQTT 3.1.1 or 5.0 |
| Keep Alive (seconds) | Heartbeat interval |
| Connection Timeout (seconds) | Max time to establish connection |
| Persistent Session | Resume subscription state after reconnect |

#### 3 Advanced
| Setting | Description | Default |
|---------|-------------|---------|
| Message Buffer Size | Max messages held in memory awaiting flow processing | 1000 |
| Client ID Override | Custom MQTT client ID (default: `decisions-mqtt-{queueId}`). Always applied regardless of the Override Global Settings toggle. | — |
| Shared Subscription Group | Consumer group name for MQTT 5.0 shared subscriptions (e.g. `decisions`). Only visible when protocol version 5.0 is active. | — |

#### 4 Last Will and Testament
| Setting | Description |
|---------|-------------|
| Enable Last Will | Publish a message automatically if Decisions disconnects unexpectedly |
| Last Will Topic | Topic to publish the LWT message to |
| Last Will Payload | Message content |
| Last Will QoS | QoS level for the LWT message |
| Last Will Retain | Whether the broker should retain the LWT message |

## Flow Steps

The module registers three steps under **Integration/MQTT** in the Decisions flow designer.

### Publish MQTT Message
Publishes a text message to an MQTT topic using the connection settings of the selected queue.

**Inputs:**
| Input | Required | Description |
|-------|----------|-------------|
| Message | Yes | The message payload to publish |
| Topic Override | No | Override the queue's default topic for this publish |
| Retain | No | Set the MQTT retain flag on the message |

**Step configuration:**
- **Queue** — select which configured MQTT queue to use for the broker connection
- **QoS Override** — override the queue's default QoS level for this step

**Outcome:** Done

> Note: Publishing to wildcard topics (`+`, `#`) is not allowed. Specify a concrete topic.

### Enable MQTT Queue
Enables a previously disabled MQTT queue, resuming message processing.

**Outcome:** Done

### Disable MQTT Queue
Disables an MQTT queue, pausing message processing without removing the queue definition.

**Outcome:** Done

## MQTT 5.0 Features

Select **Protocol Version: 5.0** in global settings or per-queue connection settings to enable MQTT 5.0 features.

### Persistent Sessions
With MQTT 5.0 and `Persistent Session = true`, the broker retains the subscription state between reconnects using `SessionExpiryInterval = MaxValue`. Missed messages published at QoS 1 or 2 during a disconnect are delivered on reconnect.

> **Note:** Persistent sessions are disabled by default. If you enable them, use a fixed **Client ID Override** — the broker identifies your session by client ID, and if the queue is recreated with a new auto-generated ID the broker's stored session becomes orphaned.

### Shared Subscriptions
Normally in MQTT every subscriber receives a copy of each message. In a Decisions cluster this means all nodes would process every message — which is rarely desirable.

Shared subscriptions solve this: the broker distributes messages round-robin across all nodes in the group so each message is processed exactly once, regardless of how many nodes are running.

Set **Shared Subscription Group** to a group name (e.g. `decisions`) and the module subscribes as `$share/decisions/{topic}`. When a group name is configured, **all cluster nodes connect simultaneously** and the broker handles load balancing. The single-node lease mechanism is automatically bypassed in this mode.

```
Publisher → Broker → $share/decisions/sensors/#
                          ├── Node A  ← message 1, 4, 7...
                          ├── Node B  ← message 2, 5, 8...
                          └── Node C  ← message 3, 6, 9...
```

> **Note:** Shared subscriptions require Protocol Version 5.0 and broker support (e.g. Mosquitto 2.0+, EMQX, HiveMQ). The Shared Subscription Group field is only visible in the queue configuration when the effective protocol version is 5.0.

#### Message Loss Risk
- **QoS 0:** Messages can be lost if a node goes down before the flow completes. The broker does not retry QoS 0 delivery.
- **QoS 1 or 2:** The broker holds the message until the client sends an ACK. If a node goes down before ACKing, most modern brokers re-deliver the message to another node in the group. Use QoS 1 or 2 for guaranteed delivery in shared subscription mode.

#### Client IDs in Shared Subscription Mode
Each Decisions process automatically gets a unique client ID (`decisions-mqtt-{queueId}-{machineName}-{processGuid}`) to avoid broker conflicts — even when multiple Decisions processes run on the same machine. Clean sessions are used so the broker redistributes messages rather than queuing them for a specific reconnecting node.

### User Properties as Flow Headers
MQTT 5.0 messages can carry User Properties (key/value metadata set by the publisher). The module extracts these and passes them as flow message headers with the prefix `UserProp.{name}`.

**Example:** A publisher sets User Property `deviceId = sensor-42`. In the Decisions flow handler the header `UserProp.deviceId` is available with value `sensor-42`.

## Cluster Deployments

The module supports cluster modes depending on the queue configuration:

### Lease Mode (default)
One cluster node holds the MQTT connection at a time. A database-backed lease (60-second TTL, renewed every 20 seconds) ensures exclusivity. If the active node goes down, the lease expires and another node takes over automatically.

**Use when:** MQTT 3.1.1, low-to-medium traffic, or when message ordering must be preserved.

### Shared Subscription Mode (MQTT 5.0)
All cluster nodes connect to the broker simultaneously. The broker distributes messages across nodes using the shared subscription group. No lease is used — each node processes its share independently.

**Use when:** High-throughput topics in a multi-node cluster where parallel processing is desired.

| | Lease Mode | Shared Subscription Mode |
|---|---|---|
| Protocol | 3.1.1 or 5.0 | 5.0 only |
| Active nodes | 1 | All |
| Message distribution | One node gets all | Broker round-robins |
| Failover | Automatic (lease expiry) | Automatic (broker reconnect) |
| Configuration | Default | Set Shared Subscription Group |

### Connection Loss and Restart
If the broker connection drops unexpectedly, the module detects it via the MQTT disconnect event and raises an exception on the next receive cycle. Decisions catches this and automatically restarts the worker thread, which reconnects to the broker. No manual intervention is required.

## Monitoring

The module logs under the following log categories:

| Category | Description |
|----------|-------------|
| `MQTT` | Utility/shared operations, settings access |
| `MQTT Settings` | Global and project settings |
| `MQTT Flow Worker` | Queue worker thread — message receive, lease, connection events |
| `MQTT Step` | Flow step execution — publish, enable, disable |

### Log Levels
- **ERROR** — Connection failures, publish errors, unhandled exceptions
- **WARN** — Connection loss, message buffer full (messages dropped), lease changes
- **INFO** — Lease acquisition/release, queue start/stop, connect/subscribe events
- **DEBUG** — Published messages, received messages, lease renewals

### Configure Log Level
```
System > Settings > System Settings > Log Settings
```
Set the log level for the relevant category (e.g. `MQTT Flow Worker`).

## Building from Source

### Prerequisites
- .NET 10.0 SDK or higher
- `CreateDecisionsModule` Global Tool (installed automatically during build)
- Decisions Platform SDK (NuGet package: `DecisionsSDK`)
- `Decisions.MessageQueues.dll` — copy from your Decisions installation (see below)

#### Obtaining Decisions.MessageQueues.dll

`Decisions.MessageQueues.dll` is part of the Decisions platform and is not distributed with this module. Copy it from your Decisions server installation:

```
{Decisions install path}\Decisions Server\modules\Decisions.MessageQueues\CoreServicesDlls\Decisions.MessageQueues.dll
```
or Extract it from
```
{Decisions install path}\Decisions Server\modules\Decisions.MessageQueues.zip
```

Place the file in the **repository root** (next to `Decisions.MQTT.sln`) before building. It is referenced by the project via `<HintPath>..\Decisions.MessageQueues.dll</HintPath>` and is excluded from source control.

### Build Steps

#### On Linux/macOS:
```bash
chmod +x build_module.sh
./build_module.sh
```

#### On Windows (PowerShell):
```powershell
.\build_module.ps1
```

#### Manual Build:
```bash
# 1. Publish the project
dotnet publish ./Decisions.MQTT/Decisions.MQTT.csproj --self-contained false --output ./Decisions.MQTT/bin -c Debug

# 2. Install/Update CreateDecisionsModule tool
dotnet tool update --global CreateDecisionsModule-GlobalTool

# 3. Create the module package
CreateDecisionsModule -buildmodule Decisions.MQTT -output "." -buildfile Module.Build.json
```

### Build Output
The build creates `Decisions.MQTT.zip` in the root directory containing:
- `Decisions.MQTT.dll` — compiled module
- `MQTTnet.dll` — MQTT client library
- `mqtt-ver.png` — module icon
- Module metadata

Upload the ZIP directly to Decisions via **System > Administration > Features**.


## Disclaimer

This module is provided "as is" without warranties of any kind. Use it at your own risk. The authors, maintainers, and contributors disclaim all liability for any direct, indirect, incidental, special, or consequential damages, including data loss or service interruption, arising from the use of this software.

**Important Notes:**
- Always test in a non-production environment first
- Ensure proper monitoring and alerting for production deployments
- Review your MQTT broker's documentation for broker-specific configuration
- This module is not officially supported by Decisions
