// Copyright (c) Pixeval.
// Licensed under the GPL v3 License.

using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Pixeval.CoreApi.Model;
using Pixeval.Pages.IllustrationViewer;
using Pixeval.Pages.NovelViewer;

namespace Pixeval.Controls;

public sealed partial class DownloadView : UserControl
{
    public DownloadViewViewModel ViewModel { get; } = new(App.AppViewModel.DownloadManager.QueuedTasks);

    public DownloadView() => InitializeComponent();

    private void ItemsView_OnSelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args)
    {
        ViewModel.SelectedEntries = sender.SelectedItems.Cast<DownloadItemViewModel>().ToArray();
    }

    private async void DownloadItem_OnOpenIllustrationRequested(DownloadItem sender, DownloadItemViewModel viewModel)
    {
        switch (viewModel.Entry)
        {
            case Illustration illustration:
                await this.CreateIllustrationPageAsync(illustration.Id);
                break;
            case Novel novel:
                await this.CreateNovelPageAsync(novel.Id);
                break;
        }
    }

    private async void DownloadItem_OnViewModelChanged(DownloadItem sender, DownloadItemViewModel viewModel)
    {
        _ = await viewModel.TryLoadThumbnailAsync(ViewModel);
    }

    ~DownloadView() => ViewModel.Dispose();
}
