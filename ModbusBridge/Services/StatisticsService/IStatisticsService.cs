using System;
using ModbusBridge.Models;

namespace ModbusBridge.Services;

public interface IStatisticsService
{
    BridgeStatistics GetSnapshot();
    void RegisterModbusRequest(bool success, TimeSpan elapsed);
    void RegisterPlcWrite(bool success, TimeSpan elapsed);
}
