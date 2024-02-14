#region Copyright (c) Pixeval/Pixeval
// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2023 Pixeval/PlayWrightHelper.cs
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
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Pixeval.Util;

public class PlayWrightHelper(int remoteDebuggingPort) : IAsyncDisposable
{
    private IPlaywright Pw { get; set; } = null!;

    internal IBrowser Browser { get; set; } = null!;

    internal IPage Page { get; set; } = null!;

    private int RemoteDebuggingPort { get; } = remoteDebuggingPort;

    public async ValueTask DisposeAsync()
    {
        if (Browser == null!)
            return;
        await Browser.CloseAsync();
        Browser = null!;
        Pw.Dispose();
        GC.Collect();
    }

    public async Task Initialize()
    {
        Pw = await Playwright.CreateAsync();
        Browser = await Pw.Chromium.ConnectOverCDPAsync($"http://localhost:{RemoteDebuggingPort}");
        Page = Browser.Contexts[0].Pages[0];
    }
}
