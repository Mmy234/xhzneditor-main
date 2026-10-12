using System.Windows;
using System.Windows.Input;

namespace XhznPlayer;

public partial class PasswordWindow : Window
{
    private readonly string _expected;

    public PasswordWindow(string expected)
    {
        InitializeComponent();
        _expected = expected ?? "";
        Loaded += (_, _) => Box.Focus();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (Box.Password == _expected)
        {
            DialogResult = true;
            Close();
            return;
        }
        Hint.Text = "密码错误";
        Box.Clear();
        Box.Focus();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
        base.OnKeyDown(e);
    }
}
