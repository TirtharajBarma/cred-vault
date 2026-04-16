import { Pipe, PipeTransform } from '@angular/core';
import { DateInput, formatIstDate } from '../../core/utils/date-time.util';

@Pipe({
  name: 'istDate',
  standalone: true
})
export class IstDatePipe implements PipeTransform {
  transform(value: DateInput, pattern = 'MMM dd, yyyy', fallback = '-'): string {
    return formatIstDate(value, pattern, fallback);
  }
}
