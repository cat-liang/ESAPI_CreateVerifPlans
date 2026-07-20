using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        public Script() { } // constructor

        // variables
        private ScriptContext _context;
        private Patient _patient;
        private Course _course;
        private ExternalPlanSetup _lastPlanSetup;
        private StructureSet _structureSet;
        private int _doseRate = 600;

        private VRect<double> _jawPos2x2 = new VRect<double>(-10, -10, 10, 10);
        private VRect<double> _jawPos3x3 = new VRect<double>(-15, -15, 15, 15);

        private float[,] _MLC1x1 = new float[2, 60];
        private float[,] _MLC2x2 = new float[2, 60];

        private VVector _iso = new VVector(0, -240, 0);

        private ObservableCollection<string> _machineIds;
        private ListBox _machineIdsLB;
        private TextBox _otherEntryTextBox;
        private const string OtherSentinel = "Other";

        private List<string> _energyModes = new List<string> {"6X", "10X", "15X", "18X",
                                                              "6FFF", "10FFF",
                                                              "6E", "9E", "12E", "15E", "16E", "18E", "20E", "22E"};

        private ListBox _energyModesLB;
        private Button _createPlansButton;
        private Button _exportDoseProfilesButton;

        private Window _window;

        [MethodImpl(MethodImplOptions.NoInlining)]

        public void Execute(ScriptContext context, System.Windows.Window window)
        {            
            try
            {
                ValidatePatient(context);

                _context = context;

                // retrieve patient & plan info
                _patient = GetPatient(context);
                _course = GetCourse(context);

                _patient.BeginModifications();

                _structureSet = _patient.AddEmptyPhantom("Verification", PatientOrientation.HeadFirstSupine, 1024, 1024, 512, 512, 513, .1);

                InitializeMLCs();

                _machineIds = new ObservableCollection<string>
                {
                    "21EX",
                    "21EX_21iX",
                    "21iX_AB",
                    "21iX_CEN",
                    "6EX",
                    "Eclipse CAP",
                    "EclipseCAP_TB",
                    "TEST-Clinac",
                    "TrueBeamNorth",
                    OtherSentinel
                };

                _machineIdsLB = new ListBox
                {
                    ItemsSource = _machineIds,
                    SelectionMode = SelectionMode.Single,
                    Width = 150,
                    Height = 120
                };
                _machineIdsLB.SelectionChanged += MachineIdsLB_SelectionChanged;

                _otherEntryTextBox = new TextBox
                {
                    Width = 150,
                    Visibility = Visibility.Collapsed,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                _otherEntryTextBox.KeyDown += OtherEntryTextBox_KeyDown;
                _otherEntryTextBox.LostFocus += OtherEntryTextBox_LostFocus;

                _energyModesLB = new ListBox
                {
                    SelectionMode = SelectionMode.Multiple,
                    Width = 150,
                    Height = 120,
                    ItemsSource = _energyModes
                };

                _createPlansButton = new Button
                {
                    Content = "Create plans",
                    Padding = new Thickness(10),
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Width = 260,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                _createPlansButton.Click += CreatePlansButton_Click;

                StackPanel spMain = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(20),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                StackPanel spMachines = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                spMachines.Children.Add(new TextBlock
                {
                    Text = "Select machine: ",
                    Margin = new Thickness(0, 0, 10, 0)
                });
                spMachines.Children.Add(_machineIdsLB);
                spMachines.Children.Add(_otherEntryTextBox);

                StackPanel spEnergyOptions = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                spEnergyOptions.Children.Add(new TextBlock
                {
                    Text = "Select energies: ",
                    Margin = new Thickness(0, 0, 10, 0)
                });
                spEnergyOptions.Children.Add(_energyModesLB);

                spMain.Children.Add(spMachines);
                spMain.Children.Add(spEnergyOptions);
                spMain.Children.Add(_createPlansButton);

                _window = window;
                window.FontFamily = new System.Windows.Media.FontFamily("Calibri");
                window.FontSize = 14;
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                window.Content = spMain;
                window.SizeToContent = SizeToContent.WidthAndHeight;

            } // try
            catch (Exception ex)
            {
                throw new ApplicationException("An error occurred while executing the script.", ex);
            } // catch
        } // Execute()

        #region validators
        
        private void ValidatePatient(ScriptContext context)
        {
            if (context.Patient == null)
            {
                throw new ApplicationException("No patient is currently open. Please open a patient and try again.");
            }
        } // ValidatePatient()

        private void ValidateStructureSet(ScriptContext context)
        {
            if (context.StructureSet == null)
            {
                throw new ApplicationException("Failed to open structure set.");
            }
        } // ValidateStructureSet()

        #endregion validators

        #region getters
        private static Patient GetPatient(ScriptContext context)
        {
            return context.Patient;
        } // GetPatient()

        private static Course GetCourse(ScriptContext context)
        {
            return context.Course;
        } // GetCourse()

        private static StructureSet GetStructureSet(ScriptContext context)
        {
            return context.StructureSet;
        } // GetStructureSet()

        private static PlanSetup GetPlanSetup(ScriptContext context)
        {
            return context.PlanSetup;
        } // GetPlanSetup()

        #endregion getters

        private void InitializeMLCs()
        {
            // 1x1 --------------------------
            for (int i = 0; i < 29; i++)
            {
                _MLC1x1[0, i] = 25;
                _MLC1x1[1, i] = 25;
            } // leaf pairs 1-29

            for (int i = 29; i < 31; i++)
            {
                _MLC1x1[0, i] = -5;
                _MLC1x1[1, i] = 5;
            } // leaf pairs 30-31

            for (int i = 31; i < 60; i++)
            {
                _MLC1x1[0, i] = 25;
                _MLC1x1[1, i] = 25;
            } // leaf pairs 32-60


            // 2x2 MLCs ----------------------
            for (int i = 0; i < 28; i++)
            {
                _MLC2x2[0, i] = 30;
                _MLC2x2[1, i] = 30;
            } // leaf pairs 1-28

            for (int i = 28; i < 32; i++)
            {
                _MLC2x2[0, i] = -10;
                _MLC2x2[1, i] = 10;
            } // leaf pairs 29-32

            for (int i = 32; i < 60; i++)
            {
                _MLC2x2[0, i] = 30;
                _MLC2x2[1, i] = 30;
            } // leaf pairs 33-60

        } // InitializeMLCs()

        private void MachineIdsLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Equals(_machineIdsLB.SelectedItem, OtherSentinel))
            {
                _otherEntryTextBox.Visibility = Visibility.Visible;
                _otherEntryTextBox.Focus();
            }
            else
            {
                _otherEntryTextBox.Visibility = Visibility.Collapsed;
            }
        } // MachineIdsLB_SelectionChanged()

        private void OtherEntryTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitOtherEntry();
            }
        } // OtherEntryTextBox_KeyDown()

        private void OtherEntryTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitOtherEntry();
        } // OtherEntryTextBox_LostFocus()

        private void CommitOtherEntry()
        {
            var text = _otherEntryTextBox.Text?.Trim();

            _machineIds.Remove(OtherSentinel);

            if (!string.IsNullOrEmpty(text))
            {
                _machineIds.Add(text);
                _machineIdsLB.SelectedItem = text;
            }

            _otherEntryTextBox.Text = string.Empty;
            _otherEntryTextBox.Visibility = Visibility.Collapsed;
        } // CommitOtherEntry()

        private void CreatePlansButton_Click(object sender, RoutedEventArgs e)
        {
            if (_otherEntryTextBox.Visibility == Visibility.Visible)
            {
                CommitOtherEntry();
            }

            if (!_course.CanAddPlanSetup(_structureSet))
            {
                throw new ApplicationException("Could not add new plan.");
            } // if

            List<string> selectedEnergies = _energyModesLB.SelectedItems.Cast<string>().ToList();
            if (selectedEnergies.Count == 0)
            {
                throw new ApplicationException("No energies selected.");
            } // if

            ExternalPlanSetup extPlan = _course.AddExternalPlanSetup(_structureSet);
            _lastPlanSetup = extPlan;

            foreach (string energy in selectedEnergies)
            {
                CreateFields(extPlan, energy);
            } // foreach

            _window.Close();
        } // CreatePlansButton_Click()

        private void CreateFields(ExternalPlanSetup extPlan, string energy)
        {
            if (_machineIdsLB.SelectedItem == null)
            {
                throw new ApplicationException("No machine selected.");
            }

            string machineId = _machineIdsLB.SelectedItem as string;

            ExternalBeamMachineParameters machineParams = new ExternalBeamMachineParameters(machineId, energy, _doseRate, "STATIC", null);
            
            // 1x1 MLCs, 2x2 jaw
            Beam beamMLC1x1 = extPlan.AddMLCBeam(machineParams, _MLC1x1, _jawPos2x2, 0, 0, 0, _iso);
            beamMLC1x1.Id = energy + " 1x1MLC";

            // 2x2 MLCs, 3x3 jaw
            Beam beamMLC2x2 = extPlan.AddMLCBeam(machineParams, _MLC2x2, _jawPos3x3, 0, 0, 0, _iso);
            beamMLC2x2.Id = energy + " 2x2MLC";
            
        } // CreateFields()

    } // class Script
} // namespace VMS.TPS