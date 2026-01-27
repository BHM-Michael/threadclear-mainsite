import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
    selector: 'app-slack-install',
    standalone: true,
    imports: [CommonModule],
    templateUrl: './slack-install.component.html',
    styleUrls: ['./slack-install.component.scss']
})
export class SlackInstallComponent {

    clientId = '10213956882343.10251286890417';
    redirectUri = 'https://threadclear-functions-fsewbzcsd8fuegdj.eastus-01.azurewebsites.net/api/slack-oauth';
    scopes = 'channels:history,chat:write,commands,groups:history,im:history,mpim:history,users:read';

    get installUrl(): string {
        return `https://slack.com/oauth/v2/authorize?client_id=${this.clientId}&scope=${this.scopes}&redirect_uri=${encodeURIComponent(this.redirectUri)}`;
    }

    installSlack(): void {
        window.location.href = this.installUrl;
    }
}