import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ConversationAnalyzerComponent } from './conversation-analyzer.component';

describe('ConversationAnalyzerComponent', () => {
  let component: ConversationAnalyzerComponent;
  let fixture: ComponentFixture<ConversationAnalyzerComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [ConversationAnalyzerComponent]
    });
    fixture = TestBed.createComponent(ConversationAnalyzerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
