﻿using HidSharp;
using HidSharp.Reports;

namespace GHelper.USB;
public static class AsusHid
{
    public const int ASUS_ID = 0x0b05;

    public const byte INPUT_ID = 0x5a;
    public const byte AURA_ID = 0x5d;

    static int[] deviceIds = { 0x1a30, 0x1854, 0x1869, 0x1866, 0x19b6, 0x1822, 0x1837, 0x1854, 0x184a, 0x183d, 0x8502, 0x1807, 0x17e0, 0x18c6, 0x1abe, 0x1b4c, 0x1b6e, 0x1b2c, 0x8854 };

    static HidStream? auraStream;

    public static IEnumerable<HidDevice>? FindDevices(byte reportId)
    {
        IEnumerable<HidDevice> deviceList;

        try
        {
            deviceList = DeviceList.Local.GetHidDevices(ASUS_ID)
                .Where(device => deviceIds.Contains(device.ProductID) && device.GetMaxFeatureReportLength() > 0 && device.CanOpen);
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"Error enumerating HID devices: {ex.Message}");
            yield break;
        }

        foreach (var device in deviceList)
        {
            bool isValid = false;
            try
            {
                isValid = device.GetReportDescriptor().TryGetReport(ReportType.Feature, reportId, out _);
            }
            catch (Exception ex)
            {
                // Logger.WriteLine($"Error getting report descriptor for device {device.ProductID.ToString("X")}: {ex.Message}");
            }
            if (isValid) yield return device;
        }
    }

    public static HidStream? FindHidStream(byte reportId)
    {
        try
        {
            var devices = FindDevices(reportId);
            if (devices == null || !devices.Any())
            {
                Logger.WriteLine($"No HID devices found for report ID {reportId:X2}");
                return null;
            }

            if (AppConfig.IsZ13())
            {
                var z13 = devices.Where(device => device.ProductID == 0x1a30).FirstOrDefault();
                if (z13 != null) return z13.Open();
            }

            if (AppConfig.IsS17())
            {
                var s17 = devices.Where(device => device.ProductID == 0x18c6).FirstOrDefault();
                if (s17 != null) return s17.Open();
            }

            foreach (var device in devices)
                Logger.WriteLine($"Input available: {device.DevicePath} {device.ProductID.ToString("X")} {device.GetMaxFeatureReportLength()}");

            return devices.FirstOrDefault()?.Open();
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"Error accessing HID device: {ex.Message}");
        }

        return null;
    }

    public static void WriteInput(byte[] data, string? log = "USB")
    {
        var devices = FindDevices(INPUT_ID);
        if (devices == null || !devices.Any())
        {
            Logger.WriteLine("No input HID devices found.");
            return;
        }

        foreach (var device in devices)
        {
            try
            {
                using (var stream = device.Open())
                {
                    var payload = new byte[device.GetMaxFeatureReportLength()];
                    Array.Copy(data, payload, data.Length);
                    stream.SetFeature(payload);
                    if (log != null) Logger.WriteLine($"{log} {device.ProductID.ToString("X")}|{device.GetMaxFeatureReportLength()}: {BitConverter.ToString(data)}");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error setting feature {device.GetMaxFeatureReportLength()} {device.DevicePath}: {BitConverter.ToString(data)} {ex.Message}");
            }
        }
    }

    public static void Write(byte[] data, string log = "USB")
    {
        Write(new List<byte[]> { data }, log);
    }

    public static void Write(List<byte[]> dataList, string log = "USB")
    {
        var devices = FindDevices(AURA_ID);
        if (devices == null || !devices.Any())
        {
            Logger.WriteLine("No aura HID devices found.");
            return;
        }

        foreach (var device in devices)
        {
            try
            {
                using (var stream = device.Open())
                {
                    foreach (var data in dataList)
                    {
                        try
                        {
                            stream.Write(data);
                            if (log != null) Logger.WriteLine($"{log} {device.ProductID.ToString("X")}: {BitConverter.ToString(data)}");
                        }
                        catch (Exception ex)
                        {
                            if (log != null) Logger.WriteLine($"Error writing {log} {device.ProductID.ToString("X")}: {ex.Message} {BitConverter.ToString(data)} ");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (log != null) Logger.WriteLine($"Error opening {log} {device.ProductID.ToString("X")}: {ex.Message}");
            }
        }
    }

    public static void WriteAura(byte[] data, bool retry = true)
    {
        if (auraStream == null) auraStream = FindHidStream(AURA_ID);
        if (auraStream == null)
        {
            Logger.WriteLine("Aura stream not found");
            return;
        }

        try
        {
            auraStream.Write(data);
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"Error writing data to HID device: {ex.Message} {BitConverter.ToString(data)}");
            auraStream.Dispose();
            auraStream = null;
            if (retry) WriteAura(data, false);
        }
    }
}
