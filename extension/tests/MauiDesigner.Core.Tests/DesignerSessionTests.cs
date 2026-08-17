using System;
using System.Collections.Generic;
using System.Linq;

using MauiDesigner.Core.Manifests;
using MauiDesigner.Core.Protocol;

using Xunit;

namespace MauiDesigner.Core.Tests
{
    /// <summary>
    /// The session is the host half of the contract in `src/app/services/host-bridge.ts`.
    /// </summary>
    public class DesignerSessionTests
    {
        private readonly List<string> _posted = new List<string>();

        private DesignerSession CreateSession() => new DesignerSession(_posted.Add);

        private IEnumerable<DesignerMessage> Posted() =>
            _posted.Select(DesignerProtocol.Parse).Where(message => message is not null)!;

        private static string FromDesigner(string type, string? xaml = null, string? message = null) =>
            DesignerProtocol.Serialize(new DesignerMessage { Type = type, Xaml = xaml, Message = message });

        [Fact]
        public void Opening_a_document_before_the_designer_is_ready_replays_it()
        {
            var session = CreateSession();

            session.OpenDocument("<ContentPage />", "MainPage.xaml");
            Assert.Empty(_posted);

            session.HandleMessage(FromDesigner(MessageTypes.DesignerReady));

            var types = Posted().Select(message => message.Type).ToList();
            Assert.Equal(new[] { MessageTypes.HostReady, MessageTypes.DocumentLoad }, types);

            var load = Posted().Last();
            Assert.Equal("<ContentPage />", load.Xaml);
            Assert.Equal("MainPage.xaml", load.FileName);
        }

        [Fact]
        public void Opening_a_document_after_the_designer_is_ready_sends_it_immediately()
        {
            var session = CreateSession();
            session.HandleMessage(FromDesigner(MessageTypes.DesignerReady));
            _posted.Clear();

            session.OpenDocument("<Grid />", "Other.xaml");

            var load = Assert.Single(Posted());
            Assert.Equal(MessageTypes.DocumentLoad, load.Type);
            Assert.Equal("<Grid />", load.Xaml);
        }

        [Fact]
        public void The_host_announces_which_ide_it_is()
        {
            var session = new DesignerSession(_posted.Add, "vscode");

            session.HandleMessage(FromDesigner(MessageTypes.DesignerReady));

            Assert.Equal("vscode", Posted().First().Host);
        }

        [Fact]
        public void Designer_edits_mark_the_document_dirty()
        {
            var session = CreateSession();
            session.OpenDocument("<ContentPage />", "MainPage.xaml");

            var changes = new List<string>();
            session.DocumentChanged += (_, args) => changes.Add(args.Xaml);

            session.HandleMessage(FromDesigner(MessageTypes.DocumentChanged, "<ContentPage><Label /></ContentPage>"));

            Assert.True(session.IsDirty);
            Assert.Equal("<ContentPage><Label /></ContentPage>", session.CurrentXaml);
            Assert.Single(changes);
        }

        [Fact]
        public void An_echo_of_the_current_xaml_is_not_a_change()
        {
            var session = CreateSession();
            session.OpenDocument("<ContentPage />", "MainPage.xaml");

            var changes = 0;
            session.DocumentChanged += (_, _) => changes++;

            session.HandleMessage(FromDesigner(MessageTypes.DocumentChanged, "<ContentPage />"));

            Assert.False(session.IsDirty);
            Assert.Equal(0, changes);
        }

        [Fact]
        public void Save_requests_reach_the_host_and_clear_the_dirty_flag_once_written()
        {
            var session = CreateSession();
            session.OpenDocument("<ContentPage />", "MainPage.xaml");
            session.HandleMessage(FromDesigner(MessageTypes.DocumentChanged, "<Grid />"));

            DocumentSaveRequestedEventArgs? request = null;
            session.SaveRequested += (_, args) => request = args;

            session.HandleMessage(FromDesigner(MessageTypes.DocumentSave, "<Grid />"));

            Assert.NotNull(request);
            Assert.Equal("<Grid />", request!.Xaml);
            Assert.Equal("MainPage.xaml", request.FileName);
            Assert.True(session.IsDirty);

            session.NotifySaved();

            Assert.False(session.IsDirty);
            Assert.Contains(Posted(), message => message.Type == MessageTypes.DocumentSaved);
        }

        [Fact]
        public void A_save_without_xaml_falls_back_to_the_last_known_document()
        {
            var session = CreateSession();
            session.OpenDocument("<ContentPage />", "MainPage.xaml");

            DocumentSaveRequestedEventArgs? request = null;
            session.SaveRequested += (_, args) => request = args;

            session.HandleMessage(FromDesigner(MessageTypes.DocumentSave));

            Assert.Equal("<ContentPage />", request!.Xaml);
        }

        [Fact]
        public void Manifest_requests_are_surfaced_to_the_host()
        {
            var session = CreateSession();
            var requested = 0;
            session.ManifestsRequested += (_, _) => requested++;

            session.HandleMessage(FromDesigner(MessageTypes.ManifestsRequest));

            Assert.Equal(1, requested);
        }

        [Fact]
        public void Manifests_are_pushed_in_the_shape_the_designer_expects()
        {
            var session = CreateSession();

            session.PushManifests(new[]
            {
                new CustomControlManifest
                {
                    Id = "contoso",
                    Package = "Contoso.Maui.Controls",
                    Xmlns = new CustomNamespace { Prefix = "contoso", Uri = "clr-namespace:Contoso" },
                    Controls = { new CustomControlDefinition { Tag = "RatingBar" } }
                }
            });

            var message = Assert.Single(Posted());
            Assert.Equal(MessageTypes.ManifestsPush, message.Type);
            Assert.Equal("contoso", message.Manifests!.Single().Xmlns.Prefix);
            Assert.Contains("\"tag\":\"RatingBar\"", _posted.Single());
        }

        [Fact]
        public void Designer_errors_are_surfaced_to_the_host()
        {
            var session = CreateSession();
            string? reported = null;
            session.ErrorReported += (_, message) => reported = message;

            session.HandleMessage(FromDesigner(MessageTypes.DesignerError, message: "Malformed XAML"));

            Assert.Equal("Malformed XAML", reported);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not json")]
        [InlineData("{}")]
        [InlineData("{\"type\":\"something.unknown\"}")]
        public void Malformed_or_unknown_messages_are_ignored(string? json)
        {
            var session = CreateSession();

            var exception = Record.Exception(() => session.HandleMessage(json));

            Assert.Null(exception);
            Assert.Empty(_posted);
            Assert.False(session.IsDirty);
        }

        [Fact]
        public void A_session_needs_a_transport()
        {
            Assert.Throws<ArgumentNullException>(() => new DesignerSession(null!));
        }
    }
}
