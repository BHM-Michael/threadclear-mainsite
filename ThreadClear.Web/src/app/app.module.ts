import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { ConversationAnalyzerComponent } from './components/conversation-analyzer/conversation-analyzer.component';
import { ResultsDisplayComponent } from './components/results-display/results-display.component';
import { HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { LoginComponent } from './components/login/login.component';
import { AdminComponent } from './components/admin/admin.component';
import { RegisterComponent } from './components/register/register.component';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { OrganizationSettingsComponent } from './components/organization-settings/organization-settings.component';

import { OrganizationService } from './services/organization.service';
import { TaxonomyService } from './services/taxonomy.service';
import { InsightsService } from './services/insights.service';
import { RegistrationService } from './services/registration.service';
import { AdminService } from './services/admin.service';
import { HighlightSpellingPipe } from './pipes/highlight-spelling.pipe';
import { ProfileComponent } from './components/profile/profile.component';

@NgModule({
  declarations: [
    AppComponent,
    ConversationAnalyzerComponent,
    ResultsDisplayComponent,
    LoginComponent,
    AdminComponent,
    RegisterComponent,
    DashboardComponent,
    OrganizationSettingsComponent,
    HighlightSpellingPipe,
    ProfileComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    HttpClientModule,
    FormsModule
  ],
  providers: [
    OrganizationService,
    TaxonomyService,
    InsightsService,
    RegistrationService,
    AdminService
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
