using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using Windows.Graphics;
using Windows.Storage;
using Microsoft.UI.Windowing;

namespace DrcomLoginApp
{

    public sealed partial class MainWindow : Window
    {
        private readonly string loginUrlTemplate = "http://172.16.253.3:801/eportal/?c=Portal&a=login&callback=dr1003&login_method=1&user_account={0}&user_password={1}&wlan_user_ip={2}&wlan_user_ipv6=&wlan_user_mac={3}&wlan_ac_ip=172.16.253.1&wlan_ac_name={4}&jsVersion=3.3.2&v=4946";
        private readonly string logoutUrlTemplate = "http://172.16.253.3:801/eportal/?c=Portal&a=logout&callback=dr1004&login_method=1&user_account=drcom&user_password=123&ac_logout=0&register_mode=1&wlan_user_ip={0}&wlan_user_ipv6=&wlan_vlan_id=0&wlan_user_mac=000000000000&wlan_ac_ip=172.16.253.1&wlan_ac_name=&jsVersion=3.3.2&v=3484";

        public MainWindow()
        {
            this.InitializeComponent();
            if(LoadSavedCredentials())
            {
                login();
            }
            // ��ȡ AppWindow ����
            var appWindow = GetAppWindowForCurrentWindow();
            // �Զ��������
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(null); // ����Ĭ�ϵı�����
            // ���ô��ڵĿ�Ⱥ͸߶�
            appWindow.Resize(new SizeInt32(360, 550));
            //����icon
            appWindow.SetIcon("Assets/logo.ico");
            //��ʾ������ַ����
            IpAddressTextBlock.Text = $"IP ��ַ: {GetNetworkDetails().IpAddress}";
            InterfaceTypeTextBlock.Text = $"��������: {GetNetworkDetails().InterfaceType}";
            // ����У����Ϣ
            string savedCampus = LoadCampus();
            if (!string.IsNullOrEmpty(savedCampus))
            {
                foreach (ComboBoxItem item in campus.Items)
                {
                    if (item.Tag as string == savedCampus)
                    {
                        campus.SelectedItem = item;
                        break;
                    }
                }
            }
        }
        private AppWindow GetAppWindowForCurrentWindow()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(windowId);
        }

        private void PasswordBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            // ��鰴�µ��Ƿ��� Enter ��
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                // ���õ�¼����
                login();
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            login();
        }
        private async void login()
        {
            string account = AccountTextBox.Text.Trim();
            string password = PasswordBox.Password.Trim();
            string ip = GetNetworkDetails().IpAddress;
            string mac = GetNetworkDetails().MacAddress;
            string InterfaceType = GetNetworkDetails().InterfaceType;
            string url = "";
            string acName = (campus.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
            {
                StatusTextBlock.Text = "�������˺ź����룡";
                return;
            }
            if (InterfaceType != "none")
            {
                if (InterfaceType == "Ethernet")
                {
                    url = string.Format(loginUrlTemplate, Uri.EscapeDataString(account), Uri.EscapeDataString(password), ip, mac, "");
                }
                if (InterfaceType == "Wireless")
                {
                    url = string.Format(loginUrlTemplate, Uri.EscapeDataString(account), Uri.EscapeDataString(password), ip, mac, acName);
                }
            }
            else
            {
                StatusTextBlock.Text = "δ��ȷʶ�����������豸���볢������������Ӧ��";
                return;
            }

            try
            {
                using HttpClient client = new();
                string response = await client.GetStringAsync(url);

                if (response.Contains("\"result\":\"1\""))
                {
                    StatusTextBlock.Text = "��¼�ɹ���";
                    if (RememberMeCheckBox.IsChecked == true)
                    {
                        SaveCampus(acName);
                        SaveCredentials(account, password);
                    }
                }
                else if (response.Contains("\"ret_code\":2"))
                {
                    if (RememberMeCheckBox.IsChecked == true)
                    {
                        SaveCampus(acName);
                        SaveCredentials(account, password);
                    }
                    StatusTextBlock.Text = "��ǰ�豸�����ߣ������ظ���¼";
                }
                else
                {
                    // ������������
                    string errorMessage = response;
                    StatusTextBlock.Text = $"��¼ʧ�ܣ������ַ��{url} ������Ϣ: {errorMessage}";
                    if (RememberMeCheckBox.IsChecked == true)
                    {
                        SaveCampus(acName);
                        SaveCredentials(account, password);
                    }
                }
            }
            catch (Exception ex)
            {
                if (RememberMeCheckBox.IsChecked == true)
                {
                    SaveCampus(acName);
                    SaveCredentials(account, password);
                }
                StatusTextBlock.Text = $"����ʧ��: {ex.Message}";
            }
        }
        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            string ip = GetNetworkDetails().IpAddress;
            string mac = GetNetworkDetails().MacAddress;
            string url = string.Format(logoutUrlTemplate, ip);

            try
            {
                using HttpClient client = new();
                string response = await client.GetStringAsync(url);

                // ��ȡ JSON ���ݲ��֣�ȥ�� callback ��װ
                string json = response.Substring(response.IndexOf('(') + 1);
                json = json.Substring(0, json.LastIndexOf(')'));

                // ���� JSON
                var logoutResult = System.Text.Json.JsonDocument.Parse(json).RootElement;

                string result = logoutResult.GetProperty("result").GetString();
                string message = logoutResult.GetProperty("msg").GetString();

                // ת�� Unicode ��ϢΪ�����ı�
                message = System.Text.RegularExpressions.Regex.Unescape(message);

                if (result == "1")
                {
                    // �ǳ��ɹ�
                    StatusTextBlock.Text = "�ǳ��ɹ���";
                }
                else
                {
                    // �ǳ�ʧ�ܣ���ʾ���������Ϣ
                    StatusTextBlock.Text = $"�ǳ�ʧ�ܣ�������Ϣ: {message}";
                }
            }
            catch (Exception ex)
            {
                // ���񲢴����쳣
                StatusTextBlock.Text = $"����ʧ�ܣ�������Ϣ: {ex.Message}";
            }
        }
        private (string IpAddress, string MacAddress, string InterfaceType) GetNetworkDetails()
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && (n.NetworkInterfaceType == NetworkInterfaceType.Ethernet || n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) // ������̫������������
                            && !n.Description.ToLower().Contains("virtual") // �ų���������
                            && !n.Description.ToLower().Contains("vmware") // �ų� VMware ����
                            && !n.Description.ToLower().Contains("hyper-v")); // �ų� Hyper-V ����

            foreach (var netInterface in networkInterfaces)
            {
                var ipProps = netInterface.GetIPProperties();
                var ipv4Address = ipProps.UnicastAddresses
                    .FirstOrDefault(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                if (ipv4Address != null)
                {
                    // ��ȡʵ�ʵ� MAC ��ַ��ȥ��ð��
                    string macAddress = string.Join("", netInterface.GetPhysicalAddress()
                        .GetAddressBytes()
                        .Select(b => b.ToString("X2")));

                    // ��ȡ�������ͣ����߻����ߣ�
                    string interfaceType = netInterface.NetworkInterfaceType switch
                    {
                        NetworkInterfaceType.Ethernet => "Ethernet",
                        NetworkInterfaceType.Wireless80211 => "Wireless",
                        _ => "Other"
                    };

                    return (ipv4Address.Address.ToString(), macAddress, interfaceType);
                }
            }

            // ���û���ҵ���Ч�� IP ��ַ�� MAC ��ַ������Ĭ��ֵ
            return ("0.0.0.0", "000000000000", "none");
        }

        private void SaveCredentials(string account, string password)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["Account"] = account;
            localSettings.Values["Password"] = password;
        }

        private bool LoadSavedCredentials()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values.TryGetValue("Account", out object accountObj) &&
                localSettings.Values.TryGetValue("Password", out object passwordObj))
            {
                AccountTextBox.Text = accountObj as string ?? "";
                PasswordBox.Password = passwordObj as string ?? "";
                RememberMeCheckBox.IsChecked = true;
                return true;
            }
            return false;
        }

        private void SaveCampus(string campusTag)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["Campus"] = campusTag;
        }

        private string LoadCampus()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values.TryGetValue("Campus", out object campusObj))
            {
                return campusObj as string ?? "";
            }
            return "";
        }


    }
}
