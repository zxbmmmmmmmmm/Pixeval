#region Copyright (c) Pixeval/Pixeval
// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2022 Pixeval/IllustrationViewerPage.xaml.cs
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage.Streams;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Pixeval.AppManagement;
using Pixeval.Messages;
using Pixeval.Misc;
using Pixeval.Options;
using Pixeval.Popups;
using Pixeval.Util;
using Pixeval.Util.IO;
using Pixeval.Util.UI;
using Pixeval.Utilities;
using AppContext = Pixeval.AppManagement.AppContext;
using Windows.Graphics;
using Pixeval.Util.Threading;
using IllustrationViewModel = Pixeval.UserControls.IllustrationView.IllustrationViewModel;

namespace Pixeval.Pages.IllustrationViewer;

public sealed partial class IllustrationViewerPage : ISupportCustomTitleBarDragRegion
{
    private AppPopup? _commentRepliesPopup;

    // Tags for IllustrationInfoAndCommentsNavigationView

    private NavigationViewTag? _relatedWorksTag;

    private NavigationViewTag? _commentsTag;

    private NavigationViewTag? _illustrationInfoTag;

    private IllustrationViewerPageViewModel _viewModel = null!;

    public IllustrationViewerPage()
    {
        InitializeComponent();
        var dataTransferManager = UIHelper.GetDataTransferManager();
        dataTransferManager.DataRequested += OnDataTransferManagerOnDataRequested;
    }

    private void IllustrationViewerPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(RefreshDragRegionMessage.Shared);
        SidePanelShadow.Receivers.Add(IllustrationPresenterDockPanel);
        PopupShadow.Receivers.Add(IllustrationInfoAndCommentsSplitView);
    }

    public override void OnPageDeactivated(NavigatingCancelEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new NavigatingFromIllustrationViewerMessage());
        foreach (var imageViewerPageViewModel in _viewModel.ImageViewerPageViewModels!)
        {
            imageViewerPageViewModel.ImageLoadingCancellationHandle.Cancel();
        }

        _viewModel.Dispose();
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    public override void OnPageActivated(NavigationEventArgs e)
    {
        if (ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation") is { } animation)
        {
            animation.Configuration = new DirectConnectedAnimationConfiguration();
            animation.TryStart(IllustrationImageShowcaseFrame);
        }

        if (e.Parameter is IllustrationViewerPageViewModel viewModel)
        {
            _viewModel = viewModel.IsDisposed ? viewModel.CreateNew() : viewModel;
        }

        _relatedWorksTag = new NavigationViewTag(typeof(RelatedWorksPage), _viewModel);
        _illustrationInfoTag = new NavigationViewTag(typeof(IllustrationInfoPage), _viewModel);
        _commentsTag = new NavigationViewTag(typeof(CommentsPage), (App.AppViewModel.MakoClient.IllustrationComments(_viewModel.IllustrationId).Where(c => c is not null), _viewModel.IllustrationId));

        IllustrationImageShowcaseFrame.Navigate(typeof(ImageViewerPage), _viewModel.Current);

        WeakReferenceMessenger.Default.Send(new MainPageFrameSetConnectedAnimationTargetMessage(_viewModel.IllustrationView?.GetItemContainer(_viewModel.IllustrationViewModelInTheGridView!) ?? App.AppViewModel.AppWindowRootFrame));
        WeakReferenceMessenger.Default.TryRegister<IllustrationViewerPage, CommentRepliesHyperlinkButtonTappedMessage>(this, CommentRepliesHyperlinkButtonTapped);
    }

    private static void CommentRepliesHyperlinkButtonTapped(IllustrationViewerPage recipient, CommentRepliesHyperlinkButtonTappedMessage message)
    {
        var commentRepliesBlock = new CommentRepliesBlock
        {
            ViewModel = new CommentRepliesBlockViewModel(message.Sender!.GetDataContext<CommentBlockViewModel>())
        };
        commentRepliesBlock.CloseButtonTapped += recipient.CommentRepliesBlock_OnCloseButtonTapped;
        recipient._commentRepliesPopup = PopupManager.CreatePopup(commentRepliesBlock, widthMargin: 200, maxWidth: 1500, minWidth: 400, maxHeight: 1200, closing: (_, _) => recipient._commentRepliesPopup = null);
        PopupManager.ShowPopup(recipient._commentRepliesPopup);
    }

    private void CommentRepliesBlock_OnCloseButtonTapped(object? sender, TappedRoutedEventArgs e)
    {
        if (_commentRepliesPopup is not null)
        {
            PopupManager.ClosePopup(_commentRepliesPopup);
        }
    }

    private async void OnDataTransferManagerOnDataRequested(DataTransferManager _, DataRequestedEventArgs args)
    {
        // all the illustrations in _viewModels only differ in different image sources
        var vm = _viewModel.Current.IllustrationViewModel;
        if (_viewModel.Current.LoadingOriginalSourceTask is not { IsCompletedSuccessfully: true })
        {
            return;
        }

        var request = args.Request;
        var deferral = request.GetDeferral();
        var props = request.Data.Properties;
        var webLink = MakoHelper.GenerateIllustrationWebUri(vm.Id);

        props.Title = IllustrationViewerPageResources.ShareTitleFormatted.Format(vm.Id);
        props.Description = vm.Illustration.Title;
        props.Square30x30Logo = RandomAccessStreamReference.CreateFromStream(await AppContext.GetAssetStreamAsync("Images/logo44x44.ico"));

        var thumbnailStream = await _viewModel.Current.IllustrationViewModel.GetThumbnail(ThumbnailUrlOption.SquareMedium);
        var file = await AppKnownFolders.CreateTemporaryFileWithRandomNameAsync(_viewModel.IsUgoira ? "gif" : "png");

        if (_viewModel.Current.OriginalImageStream is { } stream)
        {
            await stream.SaveToFileAsync(file);

            props.Thumbnail = RandomAccessStreamReference.CreateFromStream(thumbnailStream);

            request.Data.SetStorageItems(Enumerates.ArrayOf(file), true);
            request.Data.SetWebLink(webLink);
            request.Data.SetApplicationLink(MakoHelper.GenerateIllustrationAppUri(vm.Id));
        }

        deferral.Complete();
    }

    private void NextImage()
    {
        IllustrationImageShowcaseFrame.Navigate(typeof(ImageViewerPage), _viewModel.Next(), new SlideNavigationTransitionInfo
        {
            Effect = SlideNavigationTransitionEffect.FromRight
        });
    }

    private void PrevImage()
    {
        IllustrationImageShowcaseFrame.Navigate(typeof(ImageViewerPage), _viewModel.Prev(), new SlideNavigationTransitionInfo
        {
            Effect = SlideNavigationTransitionEffect.FromLeft
        });
    }

    private void NextIllustration()
    {
        var illustrationViewModel = (IllustrationViewModel) _viewModel.ContainerGridViewModel!.DataProvider.IllustrationsView[_viewModel.IllustrationIndex!.Value + 1];
        var viewModel = illustrationViewModel.GetMangaIllustrationViewModels().ToArray();

        ParentFrame.Navigate(typeof(IllustrationViewerPage), new IllustrationViewerPageViewModel(_viewModel.IllustrationView!, viewModel), new SlideNavigationTransitionInfo
        {
            Effect = SlideNavigationTransitionEffect.FromRight
        });
    }

    private void PrevIllustration()
    {
        var illustrationViewModel = (IllustrationViewModel) _viewModel.ContainerGridViewModel!.DataProvider.IllustrationsView[_viewModel.IllustrationIndex!.Value - 1];
        var viewModel = illustrationViewModel.GetMangaIllustrationViewModels().ToArray();

        ParentFrame.Navigate(typeof(IllustrationViewerPage), new IllustrationViewerPageViewModel(_viewModel.IllustrationView!, viewModel), new SlideNavigationTransitionInfo
        {
            Effect = SlideNavigationTransitionEffect.FromLeft
        });
    }

    private void NextImageAppBarButton_OnTapped(object sender, TappedRoutedEventArgs e)
    {
        NextImage();
    }

    private void PrevImageAppBarButton_OnTapped(object sender, TappedRoutedEventArgs e)
    {
        PrevImage();
    }

    private void NextIllustrationAppBarButton_OnTapped(object sender, TappedRoutedEventArgs e)
    {
        NextIllustration();
    }

    private void PrevIllustrationAppBarButton_OnTapped(object sender, TappedRoutedEventArgs e)
    {
        PrevIllustration();
    }

    private void GenerateLinkToThisPageButtonTeachingTip_OnActionButtonClick(TeachingTip sender, object args)
    {
        _viewModel.IsGenerateLinkTeachingTipOpen = false;
        App.AppViewModel.AppSetting.DisplayTeachingTipWhenGeneratingAppLink = false;
    }



    private void IllustrationInfoAndCommentsNavigationView_OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is NavigationViewItem { Tag: NavigationViewTag tag })
        {
            IllustrationInfoAndCommentsFrame.Navigate(tag.NavigateTo, tag.Parameter, args.RecommendedNavigationTransitionInfo);
        }
    }

    private void IllustrationInfoAndCommentsSplitView_OnPaneOpened(SplitView sender, object args)
    {
        WeakReferenceMessenger.Default.Send(RefreshDragRegionMessage.Shared);
    }

    private void IllustrationInfoAndCommentsSplitView_OnPaneClosed(SplitView sender, object args)
    {
        WeakReferenceMessenger.Default.Send(RefreshDragRegionMessage.Shared);
    }

    public async Task<RectInt32[]> SetTitleBarDragRegionAsync(FrameworkElement? titleBar, ColumnDefinition[]? dragRegion)
    {
        await ThreadingHelper.SpinWaitAsync(() => IllustrationViewerCommandBar.ActualHeight == 0);
        const int leftButtonWidth = 50;
        var scaleFactor = UIHelper.GetScaleAdjustment();
        var point = IllustrationViewerCommandBar.TransformToVisual(this).TransformPoint(new Point(0, 0));
        var leftDragRegionStart = leftButtonWidth * scaleFactor;
        var leftDragRegionWidth = point.X * scaleFactor - leftDragRegionStart;
        var height = IllustrationViewerCommandBar.ActualHeight * scaleFactor;
        var rightDragRegionStart = leftDragRegionStart + leftDragRegionWidth + IllustrationViewerCommandBar.ActualWidth * scaleFactor;
        var rightDragRegionWidth = ActualWidth * scaleFactor - rightDragRegionStart;
        RectInt32 dragRegionL;
        dragRegionL.X = (int) leftDragRegionStart;
        dragRegionL.Y = 0;
        dragRegionL.Width = (int) leftDragRegionWidth;
        dragRegionL.Height = (int) height;
        RectInt32 dragRegionR;
        dragRegionR.X = (int) rightDragRegionStart;
        dragRegionR.Y = 0;
        dragRegionR.Width = (int) rightDragRegionWidth;
        dragRegionR.Height = (int) height;

        var list = new List<RectInt32>();
        if (!_viewModel.IsInfoPaneOpen)
        {
            list.Add(dragRegionL);
        }

        list.Add(dragRegionR);
        return list.ToArray();
    }
}