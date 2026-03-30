import { Component, Input } from '@angular/core';

@Component({
  selector: 'shared-card',
  standalone: true,
  template: '<div class="card"><ng-content></ng-content></div>'
})
export class CardComponent {
  @Input() title: string = '';
  @Input() elevated: boolean = false;
}
