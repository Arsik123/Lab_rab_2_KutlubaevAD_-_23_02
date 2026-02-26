using System.Windows;
using Lab_rab_2_KutlubaevAD_БПИ_23_02.ViewModel;

namespace Lab_rab_2_KutlubaevAD_БПИ_23_02
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _vm.Shutdown();

            base.OnClosing(e);

            System.Environment.Exit(0);
        }
    }
}
