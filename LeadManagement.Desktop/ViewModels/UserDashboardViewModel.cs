using System.Collections.ObjectModel;
using System.Windows.Input;
using LeadManagement.Desktop.Models;

namespace LeadManagement.Desktop.ViewModels
{
    public class UserDashboardViewModel : BaseViewModel
    {
        private string _agentName = "Agent";
        private string _currentUserView = "UserDashboard";
        private bool _isSidebarCollapsed = false;

        // Metric cards for the agent
        private string _assignedLeadsCount = "48";
        private string _pendingCallsCount = "19";
        private string _qualifiedLeadsCount = "14";
        private string _followUpsCount = "15";

        public UserDashboardViewModel()
        {
            NavigateUserCommand = new RelayCommand(param =>
            {
                if (param is string dest && !string.IsNullOrWhiteSpace(dest))
                {
                    CurrentUserView = dest;
                }
            });

            ToggleSidebarCommand = new RelayCommand(() => IsSidebarCollapsed = !IsSidebarCollapsed);

            // Mock initial assigned leads for preview
            MockAssignedLeads = new ObservableCollection<LeadModel>
            {
                new LeadModel
                {
                    Id = 1,
                    BusinessName = "Blue Sky Bistro & Cafe",
                    ContactName = "Sarah Jenkins",
                    Title = "General Manager",
                    CellNumber = "4025550192",
                    City = "Omaha",
                    State = "NE",
                    Status = "New Lead"
                },
                new LeadModel
                {
                    Id = 2,
                    BusinessName = "Midwest Logistics Solutions",
                    ContactName = "David Miller",
                    Title = "Operations Director",
                    CellNumber = "4025553819",
                    City = "Lincoln",
                    State = "NE",
                    Status = "Follow-up"
                },
                new LeadModel
                {
                    Id = 3,
                    BusinessName = "Prairie Valley Medical",
                    ContactName = "Dr. Robert Chen",
                    Title = "Managing Partner",
                    CellNumber = "3085559281",
                    City = "Grand Island",
                    State = "NE",
                    Status = "Qualified"
                },
                new LeadModel
                {
                    Id = 4,
                    BusinessName = "Heartland Construction Group",
                    ContactName = "Marcus Turner",
                    Title = "VP Procurement",
                    CellNumber = "4025556271",
                    City = "Bellevue",
                    State = "NE",
                    Status = "Contacted"
                }
            };
        }

        public string AgentName
        {
            get => _agentName;
            set => SetProperty(ref _agentName, value);
        }

        public string CurrentUserView
        {
            get => _currentUserView;
            set => SetProperty(ref _currentUserView, value);
        }

        public bool IsSidebarCollapsed
        {
            get => _isSidebarCollapsed;
            set
            {
                if (SetProperty(ref _isSidebarCollapsed, value))
                {
                    OnPropertyChanged(nameof(SidebarWidth));
                }
            }
        }

        public double SidebarWidth => _isSidebarCollapsed ? 68.0 : 230.0;

        public string AssignedLeadsCount
        {
            get => _assignedLeadsCount;
            set => SetProperty(ref _assignedLeadsCount, value);
        }

        public string PendingCallsCount
        {
            get => _pendingCallsCount;
            set => SetProperty(ref _pendingCallsCount, value);
        }

        public string QualifiedLeadsCount
        {
            get => _qualifiedLeadsCount;
            set => SetProperty(ref _qualifiedLeadsCount, value);
        }

        public string FollowUpsCount
        {
            get => _followUpsCount;
            set => SetProperty(ref _followUpsCount, value);
        }

        public ObservableCollection<LeadModel> MockAssignedLeads { get; }

        public ICommand NavigateUserCommand { get; }
        public ICommand ToggleSidebarCommand { get; }
    }
}

