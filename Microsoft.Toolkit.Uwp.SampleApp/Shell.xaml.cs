﻿// ******************************************************************
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THE CODE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
// THE CODE OR THE USE OR OTHER DEALINGS IN THE CODE.
// ******************************************************************

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.SampleApp.Pages;
using Microsoft.Toolkit.Uwp.UI;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Windows.System;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Microsoft.Toolkit.Uwp.UI.Extensions;

namespace Microsoft.Toolkit.Uwp.SampleApp
{
    public sealed partial class Shell
    {
        public static Shell Current { get; private set; }

        private bool _isPaneOpen;
        private Sample _currentSample;
        private AutoSuggestBox _searchBox;
        private Button _searchButton;
        private bool _hamburgerMenuClosing = false;

        private Compositor _compositor;
        private float _defaultShowAnimationDuration = 300;
        private float _defaultHideAnimationDiration = 150;

        public bool DisplayWaitRing
        {
            set { waitRing.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Shell()
        {
            InitializeComponent();

            Current = this;
        }

        public void ShowInfoArea()
        {
            InfoAreaGrid.Visibility = Visibility.Visible;
            RootGrid.ColumnDefinitions[0].Width = new GridLength(2, GridUnitType.Star);
            RootGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            RootGrid.RowDefinitions[1].Height = new GridLength(32);
            Splitter.Visibility = Visibility.Visible;
        }

        public void HideInfoArea()
        {
            InfoAreaGrid.Visibility = Visibility.Collapsed;
            RootGrid.ColumnDefinitions[1].Width = GridLength.Auto;
            RootGrid.RowDefinitions[1].Height = GridLength.Auto;
            _currentSample = null;
            CommandArea.Children.Clear();
            Splitter.Visibility = Visibility.Collapsed;
            TitleTextBlock.Text = string.Empty;
            ApplicationView.SetTitle(this, string.Empty);
        }

        public void ShowOnlyHeader(string title)
        {
            Title.Text = title;
            HideInfoArea();
        }

        /// <summary>
        /// Navigates to a Sample via a deep link.
        /// </summary>
        /// <param name="deepLink">The deep link. Specified as protocol://[collectionName]?sample=[sampleName]</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task NavigateToSampleAsync(string deepLink)
        {
            var parser = DeepLinkParser.Create(deepLink);
            var targetSample = await Samples.GetSampleByName(parser["sample"]);
            if (targetSample != null)
            {
                NavigateToSample(targetSample);
            }
        }

        public void NavigateToSample(Sample sample)
        {
            var pageType = Type.GetType("Microsoft.Toolkit.Uwp.SampleApp.SamplePages." + sample.Type);

            if (pageType != null)
            {
                InfoAreaPivot.Items.Clear();
                NavigationFrame.Navigate(pageType, sample.Name);
            }
        }

        public void RegisterNewCommand(string name, RoutedEventHandler action)
        {
            var commandButton = new Button
            {
                Content = name,
                Margin = new Thickness(10),
                Foreground = Title.Foreground,
                MinWidth = 150
            };

            commandButton.Click += action;

            CommandArea.Children.Add(commandButton);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            NavigationFrame.Navigate(typeof(About));

            // Get list of samples
            var sampleCategories = (await Samples.GetCategoriesAsync()).ToList();

            HamburgerMenu.ItemsSource = sampleCategories;

            // Options
            HamburgerMenu.OptionsItemsSource = new[]
            {
                new Option { Glyph = "\xE946", Name = "About", PageType = typeof(About) }
            };

            HideInfoArea();

            NavigationFrame.Navigating += NavigationFrame_Navigating;
            NavigationFrame.Navigated += NavigationFrameOnNavigated;
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;

            if (!string.IsNullOrWhiteSpace(e?.Parameter?.ToString()))
            {
                var parser = DeepLinkParser.Create(e.Parameter.ToString());
                var targetSample = await Sample.FindAsync(parser.Root, parser["sample"]);
                if (targetSample != null)
                {
                    NavigateToSample(targetSample);
                }
            }

            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;

            AnimationHelper.SetTopLevelShowHideAnimation(SamplePickerGrid);

            AnimationHelper.SetTopLevelShowHideAnimation(SamplePickerDetailsGrid);
            AnimationHelper.SetSecondLevelShowHideAnimation(SamplePickerDetailsGridContent);

            //ElementCompositionPreview.SetImplicitHideAnimation(ContentShadow, GetOpacityAnimation(0, 1, _defaultHideAnimationDiration));
            ElementCompositionPreview.SetImplicitShowAnimation(ContentShadow, AnimationHelper.GetOpacityAnimation(_compositor, (float)ContentShadow.Opacity, 0, _defaultShowAnimationDuration));
        }

        private async void NavigationFrame_Navigating(object sender, NavigatingCancelEventArgs navigationEventArgs)
        {
            SampleCategory category;
            if (navigationEventArgs.SourcePageType == typeof(SamplePicker) || navigationEventArgs.Parameter == null)
            {
                DataContext = null;
                _currentSample = null;
                category = navigationEventArgs.Parameter as SampleCategory;

                if (category != null)
                {
                    TrackingManager.TrackPage($"{navigationEventArgs.SourcePageType.Name} - {category.Name}");
                }

                HideInfoArea();
            }
            else
            {
                TrackingManager.TrackPage(navigationEventArgs.SourcePageType.Name);
                ShowInfoArea();

                var sampleName = navigationEventArgs.Parameter.ToString();
                _currentSample = await Samples.GetSampleByName(sampleName);
                DataContext = _currentSample;

                if (_currentSample == null)
                {
                    HideInfoArea();
                    return;
                }

                category = await Samples.GetCategoryBySample(_currentSample);
                await Samples.PushRecentSample(_currentSample);

                var propertyDesc = _currentSample.PropertyDescriptor;

                InfoAreaPivot.Items.Clear();

                if (propertyDesc != null)
                {
                    NavigationFrame.DataContext = propertyDesc.Expando;
                }

                Title.Text = _currentSample.Name;

                if (propertyDesc != null && propertyDesc.Options.Count > 0)
                {
                    InfoAreaPivot.Items.Add(PropertiesPivotItem);
                }

                if (_currentSample.HasXAMLCode)
                {
                    XamlCodeRenderer.XamlSource = this._currentSample.UpdatedXamlCode;

                    InfoAreaPivot.Items.Add(XamlPivotItem);

                    InfoAreaPivot.SelectedIndex = 0;
                }

                if (_currentSample.HasCSharpCode)
                {
                    CSharpCodeRenderer.CSharpSource = await this._currentSample.GetCSharpSourceAsync();
                    InfoAreaPivot.Items.Add(CSharpPivotItem);
                }

                if (_currentSample.HasJavaScriptCode)
                {
                    JavaScriptCodeRenderer.CSharpSource = await this._currentSample.GetJavaScriptSourceAsync();
                    InfoAreaPivot.Items.Add(JavaScriptPivotItem);
                }

                if (!string.IsNullOrEmpty(_currentSample.CodeUrl))
                {
                    GitHub.NavigateUri = new Uri(_currentSample.CodeUrl);
                    GitHub.Visibility = Visibility.Visible;
                }
                else
                {
                    GitHub.Visibility = Visibility.Collapsed;
                }

                if (_currentSample.HasDocumentation)
                {
                    InfoAreaPivot.Items.Add(DocumentationPivotItem);
                    DocumentationTextblock.Text = await this._currentSample.GetDocumentationAsync();
                }

                if (InfoAreaPivot.Items.Count == 0)
                {
                    HideInfoArea();
                }

                TitleTextBlock.Text = $"{category.Name} -> {_currentSample.Name}";
                ApplicationView.SetTitle(this, $"{category.Name} - {_currentSample.Name}");
            }

            await SetHamburgerMenuSelection();
        }

        private async Task SetHamburgerMenuSelection()
        {
            if (NavigationFrame.SourcePageType == typeof(SamplePicker))
            {
                // This is a search
                HamburgerMenu.SelectedItem = null;
                HamburgerMenu.SelectedOptionsItem = null;
            }
            else if (_currentSample != null)
            {
                var category = await Samples.GetCategoryBySample(_currentSample);

                if (HamburgerMenu.Items.Contains(category))
                {
                    HamburgerMenu.SelectedItem = category;
                    HamburgerMenu.SelectedOptionsItem = null;
                }
            }
            else
            {
                HamburgerMenu.SelectedItem = null;
                HamburgerMenu.SelectedOptionsIndex = 0;
            }
        }

        private void ExpandButton_Click(object sender, RoutedEventArgs e)
        {
            ExpandOrCloseProperties();
        }

        private void ExpandOrCloseProperties()
        {
            var states = VisualStateManager.GetVisualStateGroups(HamburgerMenu).FirstOrDefault();
            string currentState = states.CurrentState.Name;

            switch (currentState)
            {
                case "NarrowState":
                case "MediumState":
                    // If pane is open, close it
                    if (_isPaneOpen)
                    {
                        Grid.SetRowSpan(InfoAreaGrid, 1);
                        Grid.SetRow(InfoAreaGrid, 1);
                        _isPaneOpen = false;
                        ExpandButton.Content = "";
                    }
                    else
                    {
                        // pane is closed, so let's open it
                        Grid.SetRowSpan(InfoAreaGrid, 2);
                        Grid.SetRow(InfoAreaGrid, 0);
                        _isPaneOpen = true;
                        ExpandButton.Content = "";
                    }

                    break;

                case "WideState":
                    // If pane is open, close it
                    if (_isPaneOpen)
                    {
                        Grid.SetColumnSpan(InfoAreaGrid, 1);
                        Grid.SetColumn(InfoAreaGrid, 1);
                        _isPaneOpen = false;
                        ExpandButton.Content = "";
                    }
                    else
                    {
                        // Pane is closed, so let's open it
                        Grid.SetColumnSpan(InfoAreaGrid, 2);
                        Grid.SetColumn(InfoAreaGrid, 0);
                        _isPaneOpen = true;
                        ExpandButton.Content = "";
                    }

                    break;
            }
        }

        /// <summary>
        /// Called when [back requested] event is fired.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="backRequestedEventArgs">The <see cref="BackRequestedEventArgs"/> instance containing the event data.</param>
        private void OnBackRequested(object sender, BackRequestedEventArgs backRequestedEventArgs)
        {
            if (backRequestedEventArgs.Handled)
            {
                return;
            }

            if (NavigationFrame.CanGoBack)
            {
                backRequestedEventArgs.Handled = true;

                NavigationFrame.GoBack();
            }
        }

        /// <summary>
        /// When the frame has navigated this method is called.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="navigationEventArgs">The <see cref="NavigationEventArgs"/> instance containing the event data.</param>
        private void NavigationFrameOnNavigated(object sender, NavigationEventArgs navigationEventArgs)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = NavigationFrame.CanGoBack
                ? AppViewBackButtonVisibility.Visible
                : AppViewBackButtonVisibility.Collapsed;

            if (_isPaneOpen)
            {
                ExpandOrCloseProperties();
            }
        }

        private void HamburgerMenu_OnItemClick(object sender, ItemClickEventArgs e)
        {
            var category = e.ClickedItem as SampleCategory;

            if (category != null)
            {
                ShowSamplePicker(category.Samples);
            }
        }

        private void HamburgerMenu_OnOptionsItemClick(object sender, ItemClickEventArgs e)
        {
            var option = e.ClickedItem as Option;
            if (option == null)
            {
                return;
            }

            if (option.Tag != null)
            {
                NavigationFrame.Navigate(typeof(SamplePicker), option.Tag);
            }
            else if (NavigationFrame.CurrentSourcePageType != option.PageType)
            {
                NavigationFrame.Navigate(option.PageType);
            }

            HideSamplePicker();
            HamburgerMenu.IsPaneOpen = false;

            var expanders = HamburgerMenu.FindDescendants<Expander>();
            foreach (var expander in expanders)
            {
                expander.IsExpanded = false;
            }
        }

        private async void InfoAreaPivot_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InfoAreaPivot.SelectedItem != null)
            {
                var sample = DataContext as Sample;

                if (sample != null)
                {
                    TrackingManager.TrackEvent("PropertyGrid", (InfoAreaPivot.SelectedItem as FrameworkElement)?.Name, sample.Name);
                }
            }

            if (InfoAreaPivot.SelectedItem == PropertiesPivotItem)
            {
                return;
            }

            if (_currentSample == null)
            {
                return;
            }

            if (_currentSample.HasXAMLCode)
            {
                XamlCodeRenderer.XamlSource = _currentSample.UpdatedXamlCode;
            }

            if (_currentSample.HasCSharpCode)
            {
                CSharpCodeRenderer.CSharpSource = await _currentSample.GetCSharpSourceAsync();
            }

            if (_currentSample.HasJavaScriptCode)
            {
                JavaScriptCodeRenderer.JavaScriptSource = await _currentSample.GetJavaScriptSourceAsync();
            }
        }

        private async void DocumentationTextblock_OnLinkClicked(object sender, LinkClickedEventArgs e)
        {
            TrackingManager.TrackEvent("Link", e.Link);
            await Launcher.LaunchUriAsync(new Uri(e.Link));
        }

        private void DocumentationTextblock_ImageResolving(object sender, ImageResolvingEventArgs e)
        {
            e.Image = new BitmapImage(new Uri("ms-appx:///Assets/pixel.png"));
            e.Handled = true;
        }

        private async void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 700)
            {
                if (e.PreviousSize.Width > 700)
                {
                    HideSamplePicker();
                    ConnectToSearch();
                }

                if (HamburgerMenu.IsPaneOpen)
                {
                    _hamburgerMenuClosing = true;
                    await Task.Delay(800);
                    HamburgerMenu.OpenPaneLength = Window.Current.Bounds.Width;
                    _hamburgerMenuClosing = false;
                }
                else if (!_hamburgerMenuClosing)
                {
                    HamburgerMenu.OpenPaneLength = Window.Current.Bounds.Width;
                }
            }
            else if (e.PreviousSize.Width <= 700)
            {
                ConnectToSearch();
            }
        }

        private void GitHub_OnClick(object sender, RoutedEventArgs e)
        {
            TrackingManager.TrackEvent("Link", GitHub.NavigateUri.ToString());
        }

        private void ConnectToSearch()
        {
            if (_searchBox != null)
            {
                _searchBox.LostFocus -= SearchBox_LostFocus;
                _searchBox.QuerySubmitted -= SearchBox_QuerySubmitted;
                _searchBox.TextChanged -= SearchBox_TextChanged;
            }

            if (_searchButton != null)
            {
                _searchButton.Click -= SearchButton_Click;
            }

            _searchButton = HamburgerMenu.FindDescendantByName("SearchButton") as Button;
            _searchBox = HamburgerMenu.FindDescendantByName("SearchBox") as AutoSuggestBox;

            if (_searchBox == null || _searchButton == null)
            {
                return;
            }

            _searchBox.LostFocus += SearchBox_LostFocus;
            _searchBox.QuerySubmitted += SearchBox_QuerySubmitted;
            _searchBox.TextChanged += SearchBox_TextChanged;

            _searchButton.Click += SearchButton_Click;

            _searchBox.DisplayMemberPath = "Name";
            _searchBox.TextMemberPath = "Name";
        }

        private async void UpdateSearchSuggestions()
        {
            _searchBox.ItemsSource = (await Samples.FindSamplesByName(_searchBox.Text)).OrderBy(s => s.Name);
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }

            UpdateSearchSuggestions();
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var sample = args.ChosenSuggestion as Sample;
            if (sample != null)
            {
                NavigateToSample(sample);
            }
            else
            {
                NavigationFrame.Navigate(typeof(SamplePicker), _searchBox.Text);
            }

            HamburgerMenu.IsPaneOpen = false;
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            HideSamplePicker();
            _searchBox.Text = string.Empty;

            _searchButton.Visibility = Visibility.Collapsed;
            _searchBox.Visibility = Visibility.Visible;

            // We need to wait for the textbox to be created to focus it (only first time).
            TextBox innerTextbox = null;

            do
            {
                innerTextbox = _searchBox.FindDescendant<TextBox>();
                innerTextbox?.Focus(FocusState.Programmatic);

                if (innerTextbox == null)
                {
                    await Task.Delay(150);
                }
            }
            while (innerTextbox == null);
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            _searchButton.Visibility = Visibility.Visible;
            _searchBox.Visibility = Visibility.Collapsed;
        }

        private void Shell_OnLoaded(object sender, RoutedEventArgs e)
        {
            // Connect to search UI
            ConnectToSearch();
        }

        private void HideSamplePicker()
        {
            HideSampleDetails();
            SamplePickerGrid.Visibility = Visibility.Collapsed;
        }

        private void ShowSamplePicker(Sample[] samples)
        {
            SamplePickerListView.ItemsSource = samples;
            if (_currentSample != null && samples.Contains(_currentSample))
            {
                SamplePickerListView.SelectedItem = _currentSample;
            }

            SamplePickerGrid.Visibility = Visibility.Visible;
        }

        private void HideSampleDetails()
        {
            SamplePickerDetailsGrid.Visibility = Visibility.Collapsed;
        }

        private void ShowSampleDetails(Sample sample)
        {
            SamplePickerDetailsGrid.DataContext = sample;
            SamplePickerDetailsGrid.Visibility = Visibility.Visible;
        }

        private void StackPanel_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                var panel = (sender as FrameworkElement).FindDescendant<DropShadowPanel>();
                if (panel != null)
                {
                    panel.Visibility = Visibility.Visible;
                }

                ShowSampleDetails(((FrameworkElement)sender).DataContext as Sample);
            }
        }

        private void StackPanel_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            HideSampleDetails();
            var panel = (sender as FrameworkElement).FindDescendant<DropShadowPanel>();
            if (panel != null)
            {
                panel.Visibility = Visibility.Collapsed;
            }
        }

        private void SamplePickerListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            HideSamplePicker();
            NavigateToSample(e.ClickedItem as Sample);
        }

        private void VerticalSamplePickerListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            HamburgerMenu.IsPaneOpen = false;
            NavigateToSample(e.ClickedItem as Sample);
        }

        private void Expander_Expanded(object sender, EventArgs e)
        {
            var expanders = HamburgerMenu.FindDescendants<Expander>();
            foreach (var expander in expanders)
            {
                if (expander != sender)
                {
                    expander.IsExpanded = false;
                }
            }
        }

        private void HamburgerButtonClicked(object sender, RoutedEventArgs e)
        {
            HamburgerMenu.IsPaneOpen = !HamburgerMenu.IsPaneOpen;
        }

        private void ContentShadow_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            HideSamplePicker();
            SetHamburgerMenuSelection();
        }

        private void SamplePickerListView_ContainerContentChanging(Windows.UI.Xaml.Controls.ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var panel = args.ItemContainer.FindAscendant<DropShadowPanel>();
            if (panel != null)
            {
                ElementCompositionPreview.SetImplicitShowAnimation(panel, AnimationHelper.GetOpacityAnimation(_compositor, 1, 0, _defaultShowAnimationDuration));
                //ElementCompositionPreview.SetImplicitHideAnimation(panel, GetOpacityAnimation(0, _defaultHideAnimationDiration));
            }
        }

        private void SamplePickerListView_ChoosingItemContainer(Windows.UI.Xaml.Controls.ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            args.ItemContainer = args.ItemContainer ?? new ListViewItem();

            var showAnimation = AnimationHelper.GetOpacityAnimation(_compositor, 1, 0, _defaultShowAnimationDuration, 200);
            (showAnimation as ScalarKeyFrameAnimation).DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

            //ElementCompositionPreview.SetImplicitHideAnimation(args.ItemContainer, GetOpacityAnimation(0, _defaultHideAnimationDiration));
            ElementCompositionPreview.SetImplicitShowAnimation(args.ItemContainer, showAnimation);
        }
    }
}
