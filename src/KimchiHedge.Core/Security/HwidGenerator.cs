using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace KimchiHedge.Core.Security;

/// <summary>
/// HWID (Hardware ID) 생성기
/// </summary>
public class HwidGenerator
{
    /// <summary>
    /// HWID 생성
    /// </summary>
    /// <returns>SHA256 해시된 HWID</returns>
    public string GenerateHwid()
    {
        var components = new StringBuilder();

        // CPU ID
        components.Append(GetCpuId());

        // 메인보드 시리얼
        components.Append(GetMotherboardSerial());

        // 첫 번째 MAC 주소
        components.Append(GetFirstMacAddress());

        // 시스템 드라이브 시리얼
        components.Append(GetSystemDriveSerial());

        // SHA256 해시
        return ComputeSha256Hash(components.ToString());
    }

    /// <summary>
    /// CPU ID 조회
    /// </summary>
    private string GetCpuId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessorId FROM Win32_Processor");

            foreach (var obj in searcher.Get())
            {
                return obj["ProcessorId"]?.ToString() ?? string.Empty;
            }
        }
        catch
        {
            // WMI 접근 실패 시 빈 문자열 반환
        }

        return string.Empty;
    }

    /// <summary>
    /// 메인보드 시리얼 번호 조회
    /// </summary>
    private string GetMotherboardSerial()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT SerialNumber FROM Win32_BaseBoard");

            foreach (var obj in searcher.Get())
            {
                return obj["SerialNumber"]?.ToString() ?? string.Empty;
            }
        }
        catch
        {
            // WMI 접근 실패 시 빈 문자열 반환
        }

        return string.Empty;
    }

    /// <summary>
    /// 첫 번째 활성 네트워크 어댑터의 MAC 주소 조회
    /// </summary>
    private string GetFirstMacAddress()
    {
        try
        {
            var nic = NetworkInterface
                .GetAllNetworkInterfaces()
                .FirstOrDefault(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

            if (nic != null)
            {
                return nic.GetPhysicalAddress().ToString();
            }
        }
        catch
        {
            // 네트워크 인터페이스 접근 실패 시 빈 문자열 반환
        }

        return string.Empty;
    }

    /// <summary>
    /// 시스템 드라이브 시리얼 번호 조회
    /// </summary>
    private string GetSystemDriveSerial()
    {
        try
        {
            // 시스템 드라이브 (보통 C:)
            string systemDrive = Environment.GetFolderPath(Environment.SpecialFolder.System)
                .Substring(0, 2);

            using var searcher = new ManagementObjectSearcher(
                $"SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID='{systemDrive}'");

            foreach (var obj in searcher.Get())
            {
                return obj["VolumeSerialNumber"]?.ToString() ?? string.Empty;
            }
        }
        catch
        {
            // WMI 접근 실패 시 빈 문자열 반환
        }

        return string.Empty;
    }

    /// <summary>
    /// SHA256 해시 계산
    /// </summary>
    private string ComputeSha256Hash(string input)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// HWID 검증
    /// </summary>
    /// <param name="storedHwid">저장된 HWID</param>
    /// <returns>일치 여부</returns>
    public bool ValidateHwid(string storedHwid)
    {
        string currentHwid = GenerateHwid();
        return string.Equals(currentHwid, storedHwid, StringComparison.OrdinalIgnoreCase);
    }
}
