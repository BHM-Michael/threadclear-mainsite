import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

export interface SpellCheckIssue {
    word: string;
    startIndex: number;
    endIndex: number;
    type: 'spelling' | 'grammar' | 'typo';
    message: string;
    suggestions: string[];
}

@Pipe({
    name: 'highlightSpelling',
    pure: true
})
export class HighlightSpellingPipe implements PipeTransform {

    constructor(private sanitizer: DomSanitizer) { }

    transform(text: string, issues: SpellCheckIssue[] | null | undefined): SafeHtml {
        if (!text || !issues || issues.length === 0) {
            return text || '';
        }

        // Sort issues by startIndex in descending order to replace from end to start
        // This prevents index shifting as we make replacements
        const sortedIssues = [...issues].sort((a, b) => b.startIndex - a.startIndex);

        let result = text;

        for (const issue of sortedIssues) {
            const before = result.substring(0, issue.startIndex);
            const word = result.substring(issue.startIndex, issue.endIndex);
            const after = result.substring(issue.endIndex);

            const typeClass = `spell-${issue.type}`;
            const suggestions = issue.suggestions.length > 0
                ? `Suggestions: ${issue.suggestions.join(', ')}`
                : '';
            const tooltip = `${issue.message}${suggestions ? ' | ' + suggestions : ''}`;

            // Create the highlighted span
            const highlighted = `<span class="spell-issue ${typeClass}" title="${this.escapeHtml(tooltip)}" data-suggestions="${this.escapeHtml(issue.suggestions.join(','))}">${this.escapeHtml(word)}</span>`;

            result = before + highlighted + after;
        }

        return this.sanitizer.bypassSecurityTrustHtml(result);
    }

    private escapeHtml(text: string): string {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}