# CHANGELOG

**Note:** I'll try to keep the changelog up-to-date, but please be patient, I might forget it.

----

## 1.1.0 (2021-04-30)

### Added (1 change)

- Extensions to convert from/to naive data types have a parameter to `inverseRegisters` order. (by @Luke092) (Closing Issue #15 more precisely)

### Fixed (1 change)

- Better async behaviour for serial client.

### Updated (2 changes)

- Updated to C# 9.0
- Updated packages



## 1.0.4 (2021-04-13)

### Added (1 change)

- Extension to convert from/to string enhanced by switch to flip bytes (endianness). (Closing Issue #15)



## 1.0.3 (2021-03-29)

### Fixed (1 change)

- Resolved null reference errors while reconnecting (TCP and Serial). (Closing Issue #14)

### Added (1 change)

- Extensions to convert values back to `ModbusObject`s are now available.



## 1.0.2 (2020-12-08)

### Fixed (1 change)

- Removed a potential dead-lock scenario when disconnecting while on reconnect.

### Added (1 change)

- Modbus clients have a optional `CancellationToken` on `Connect()` and `Disconnect()`.



## 1.0.1 (2020-11-30)

### Fixed (1 change)

- The Modbus TCP client on unix systems creates an `IOException` on connection loss - catch that exception instead of the `EndOfStreamException`.



## 1.0.0 (2020-11-28)

### Fixed (3 changes)

##### Client

- The Modbus TCP client will now recognize (again) a connection close from the remote and start the reconnect cycle

##### Server

- The Modbus TCP server recognizes the connection close from the client as well and terminate the running tasks.
- The Modbus TCP server catches the rising `NotImplementedException`s and will not crash anymore on invalid requests.

### Added (5 changes)

- Added this changelog.
- Added a `ModbusObject` as base class for `Coil`s, `DiscreteInput`s and `Register`s. The property `Type` of `ModbusObjectType` will tell you the actual meaning.    
  For naming reasons the old classes are still present and used.
- `*.snupkg` (Debug Symbols) for the NuGet packages are available on my Gitlab.
- New package `AMWD.Modbus.Proxy`
  - *TCP to TCP* can be used when a remote device only accepts one connection but multiple clients want to request the information (or as Gateway [on a NAT device]).
  - *TCP to Serial* can be used to bring a Modbus RTU (RS485) device into your local network.
- Added a custom console logger to the console demo application.

### Changed (5 changes)

- The `Value` property of all structure classes is replaced by `RegisterValue` for 16-bit values ([Holding | Input] `Register`) and `BoolValue` for 1-bit values (`Coil`, `DiscreteInput`).
- All servers and clients can use an `ILogger` implementation for detailed logging in the instances.
- Using more `TimeSpan` for timeouts instead of integer values with different meanings (seconds, milliseconds?!?).
- Updated Gitlab CI procedure for automatic deployment to NuGet.org.
- Code cleanup and C# 8.0

### Removed (1 change)

- git attributes not needed and therefore deleted.
- `FakeSynchronizationContext` was never used.



## 0.9.10 (2020-07-07)

### Fixed (1 change)

- Changed handling with a broken serial port for a clean shutdown.



## 0.9.9 (2020-06-29)

### Fixed (2 changes)

- Fixed an issue where the serial client crashed during a request.
- SemVer tagging only allowes 3 sections (major, minor, patch).

### Added (2 changes)

- More specific evaluation of property ranges for the serial client.
- Added license information to the nuget packages.



## 0.9.8.1 (2020-06-24)

### Fixed (1 change)

- Fixed an issue with an incompatible buffer size on the serial client.



## 0.9.8 (2020-06-19)

### Fixed (2 changes)

- Requests from clients can be cancelled with a `CancelleationToken`.
- Read/Write async on serial connections is now available.    
  (The .NET implementation ignores cancellation tokens)

### Added (3 changes)

- New package to create Modbus request proxies.    
  Needed to translate requests from TCP to Serial and vice versa or to make a device with only one (1) connection at a time available to multiple clients.    
  **Note:** Not deployed on NuGet.org.
- Enhanced the console demo application to provide a server.
- Modbus servers have custom request handlers.

### Changed (2 changes)

- The clients have a new parameter to provide a `CancellationToken`.
- The `ModbusDevice` uses a more flexible `ReaderWriterLockSlim`.



## 0.9.7 (2020-05-10)

### Fixed (1 change)

- Reverted changes from a merge request to keep .NET Standard 2.0 as target framework.



## 0.9.6 (2020-05-10)

- Merging requests #10 and #11 into the `master`.

### Fixed (1 change)

- Less garbage collecting needed while reading a network stream (from [mishun](https://github.com/mishun)).

### Added (1 change)

- Additional constructor parameter to change the max. connect timeout (from [madfisht3](https://github.com/madfisht3)).



## 0.9.5 (2020-05-08)

### Fixed (2 changes)

- Serial client did not filter an error response function code.
- Reverted some incompatible package versions.

### Added (2 changes)

- `.editorconfig` added for a unified look and feel.
- `CodMaid.config` added for a unified code cleanup and structure.



## 0.9.4 (2020-02-14)

### Fixed (2 changes)

- Null reference error on disconnect fixed.
- `ReceiveLoop` not accessing a disposed stream.

### Added (2 changes)

- Logging available for the Modbus TCP server.
- Some internal extensions.

### Changed (1 change)

- Version tag on compiletime changed (NetRevisionTask).

### Removed (1 change)

- `Task.Forget()` only internal available to prevent conflicts with other implementations.



## 0.9.3 (2019-07-24)

### Fixed (2 changes)

- Catch some exceptions to keep the client more quiet.
- Fixed an issue during diconnect when the client was reconnecting.

### Added (2 changes)

- Custom console logger for UnitTests.
- More UnitTests.

### Changed (3 changes)

- Modbus TCP client uses `TaskCompletionSource`.
- Modbus Serial cient enhanced to be more async.
- Updating to C# 7.1 and more up-to-date packages.



## 0.9.2 (2019-04-12)

### Fixed (1 change)

- Modbus TCP server was not able to write multiple registers (from [hmarius](https://github.com/hmarius)).

### Added (1 change)

- Change dual driver (RS232/RS485) to RS485 state (tested with sysWORXX CTR 700).



## 0.9.1 (2019-04-02)

### Fixed (1 change)

- First working Modbus serial implementation.

### Added (3 changes)

- Implementation of function 43 (0x2B read device information).
- UnitTests.
- Gitlab CI builds.

### Changed (1 change)

- NuGet information of projects updated.



## 0.9.0 (2018-06-14) | *First Release*

- Modbus TCP Client implementation with all common functions available.
- Modbus TCP Server implementation (not well tested).
- Basic Modbus Serial implementation (not tested!).
